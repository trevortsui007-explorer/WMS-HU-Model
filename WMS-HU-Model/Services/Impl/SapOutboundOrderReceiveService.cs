using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WMS_HU_Model.Infrastructure.Sap;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Integration;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class SapOutboundOrderReceiveService : ISapOutboundOrderReceiveService
    {
        private readonly ISapOutboundOrderReceiveRepository _repository;

        public SapOutboundOrderReceiveService(ISapOutboundOrderReceiveRepository repository)
        {
            _repository = repository;
        }

        public OperationResult<SapOutboundOrderReceiveResultDto> Receive(string rawJson)
        {
            JObject payload;
            string hash;
            string error;
            if (!SapPayloadCanonicalizer.TryCreate(rawJson, out payload, out hash, out error))
            {
                _repository.RecordRejected(null, null, rawJson, error);
                return OperationResult<SapOutboundOrderReceiveResultDto>.Fail(error);
            }

            string deliveryNo = SapPayloadCanonicalizer.ExtractDeliveryNo(payload);
            if (deliveryNo == null)
            {
                const string missingDeliveryNo = "VBELN必须为非空字符串。";
                _repository.RecordRejected(null, hash, rawJson, missingDeliveryNo);
                return OperationResult<SapOutboundOrderReceiveResultDto>.Fail(missingDeliveryNo);
            }
            if (deliveryNo.Length > 50)
            {
                const string invalidDeliveryNoLength = "VBELN长度不能超过50。";
                _repository.RecordRejected(null, hash, rawJson, invalidDeliveryNoLength);
                return OperationResult<SapOutboundOrderReceiveResultDto>.Fail(invalidDeliveryNoLength);
            }

            SapOutboundOrderReceiveAttemptResult existing = _repository.TryHandleExisting(deliveryNo, hash, rawJson);
            if (existing.Exists)
            {
                return existing.IsSuccess
                    ? OperationResult<SapOutboundOrderReceiveResultDto>.Success(existing.Data, existing.Message)
                    : OperationResult<SapOutboundOrderReceiveResultDto>.Fail(existing.Message);
            }

            SapOutboundOrderReceiveRequest request;
            try
            {
                request = payload.ToObject<SapOutboundOrderReceiveRequest>(JsonSerializer.CreateDefault());
            }
            catch (JsonException)
            {
                const string invalidPayload = "SAP请求字段格式无效。";
                return RejectAfterExistingCheck(deliveryNo, hash, rawJson, invalidPayload);
            }

            SapOutboundOrderReceiveCommand command;
            string validationError = TryBuildCommand(request, deliveryNo, hash, rawJson, out command);
            if (validationError != null)
                return RejectAfterExistingCheck(deliveryNo, hash, rawJson, validationError);

            SapOutboundOrderReceiveAttemptResult result = _repository.CreateFirst(command);
            return result.IsSuccess
                ? OperationResult<SapOutboundOrderReceiveResultDto>.Success(result.Data, result.Message)
                : OperationResult<SapOutboundOrderReceiveResultDto>.Fail(result.Message);
        }

        private OperationResult<SapOutboundOrderReceiveResultDto> RejectAfterExistingCheck(
            string deliveryNo,
            string hash,
            string rawJson,
            string message)
        {
            SapOutboundOrderReceiveAttemptResult existing = _repository.TryHandleExisting(deliveryNo, hash, rawJson);
            if (existing.Exists)
            {
                return existing.IsSuccess
                    ? OperationResult<SapOutboundOrderReceiveResultDto>.Success(existing.Data, existing.Message)
                    : OperationResult<SapOutboundOrderReceiveResultDto>.Fail(existing.Message);
            }
            _repository.RecordRejected(deliveryNo, hash, rawJson, message);
            return OperationResult<SapOutboundOrderReceiveResultDto>.Fail(message);
        }

        private static string TryBuildCommand(SapOutboundOrderReceiveRequest request, string deliveryNo, string hash, string rawJson, out SapOutboundOrderReceiveCommand command)
        {
            command = null;
            if (request == null || request.T_ITEM == null || request.T_ITEM.Count == 0)
                return "T_ITEM不能为空。";

            string headerLengthError = ValidateLength(deliveryNo, 50, "VBELN")
                ?? ValidateLength(NormalizeOptional(request.KUNNR), 50, "KUNNR")
                ?? ValidateLength(NormalizeOptional(request.KUNAG), 50, "KUNAG");
            if (headerLengthError != null)
                return headerLengthError;

            DateTime? plannedShipDate;
            if (!TryParseOptionalDate(request.WADAT, out plannedShipDate))
                return "WADAT日期格式必须为yyyy-MM-dd。";

            List<SapOutboundOrderReceiveLineCommand> lines = new List<SapOutboundOrderReceiveLineCommand>();
            HashSet<string> itemNos = new HashSet<string>(StringComparer.Ordinal);
            string plantCode = null;
            foreach (SapOutboundOrderReceiveItemRequest item in request.T_ITEM)
            {
                if (item == null)
                    return "T_ITEM不能包含空对象。";
                string itemNo = NormalizeRequired(item.POSNR);
                string materialCode = NormalizeRequired(item.MATNR);
                string uom = NormalizeRequired(item.MEINS);
                string itemPlant = NormalizeRequired(item.WERKS);
                if (itemNo == null || materialCode == null || uom == null || itemPlant == null)
                    return "T_ITEM的POSNR、MATNR、MEINS和WERKS不能为空。";
                if (item.LFIMG <= 0)
                    return "T_ITEM的LFIMG必须大于0。";
                if (item.LFIMG != decimal.Round(item.LFIMG, 4) || item.LFIMG > 99999999999999.9999m)
                    return "T_ITEM的LFIMG必须符合DECIMAL(18,4)正数范围。";
                if (!itemNos.Add(itemNo))
                    return "同一SAP交货单POSNR重复。";
                if (plantCode == null)
                    plantCode = itemPlant;
                else if (!string.Equals(plantCode, itemPlant, StringComparison.Ordinal))
                    return "同一SAP交货单不允许跨WERKS。";
                string lineLengthError = ValidateLength(itemNo, 20, "POSNR")
                    ?? ValidateLength(materialCode, 50, "MATNR")
                    ?? ValidateLength(uom, 20, "MEINS")
                    ?? ValidateLength(itemPlant, 20, "WERKS")
                    ?? ValidateLength(NormalizeOptional(item.PSTYV), 20, "PSTYV")
                    ?? ValidateLength(NormalizeOptional(item.VGBEL), 50, "VGBEL")
                    ?? ValidateLength(NormalizeOptional(item.VGPOS), 20, "VGPOS")
                    ?? ValidateLength(NormalizeOptional(item.BSTKD), 100, "BSTKD");
                if (lineLengthError != null)
                    return lineLengthError;
                lines.Add(new SapOutboundOrderReceiveLineCommand
                {
                    SAPItemNo = itemNo,
                    ProdNo = materialCode,
                    RequiredQty = item.LFIMG,
                    Uom = uom,
                    SourceOrderNo = NormalizeOptional(item.VGBEL),
                    SourceOrderItem = NormalizeOptional(item.VGPOS),
                    CustomerPO = NormalizeOptional(item.BSTKD)
                });
            }

            command = new SapOutboundOrderReceiveCommand
            {
                SAPDeliveryNo = deliveryNo,
                PayloadHash = hash,
                RawJson = rawJson,
                CustomerCode = NormalizeOptional(request.KUNNR),
                SoldToCode = NormalizeOptional(request.KUNAG),
                PlannedShipDate = plannedShipDate,
                PlantCode = plantCode,
                Lines = lines
            };
            return null;
        }

        private static bool TryParseOptionalDate(string value, out DateTime? date)
        {
            date = null;
            if (string.IsNullOrWhiteSpace(value))
                return true;
            DateTime parsed;
            if (!DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return false;
            date = parsed.Date;
            return true;
        }

        private static string NormalizeRequired(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string NormalizeOptional(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string ValidateLength(string value, int maxLength, string fieldName)
        {
            return value != null && value.Length > maxLength
                ? fieldName + "长度不能超过" + maxLength + "。"
                : null;
        }
    }
}
