using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.MasterData;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class MasterDataMaintenanceService : IMasterDataMaintenanceService
    {
        private const string FinishedGoodsWarehouseType = "FINISHED_GOODS";
        private readonly IMasterDataMaintenanceRepository _repository;

        public MasterDataMaintenanceService(IMasterDataMaintenanceRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public List<WarehouseDto> QueryWarehouses(WarehouseQuery query)
        {
            query = query ?? new WarehouseQuery();
            query.WarehouseCode = Text(query.WarehouseCode, true);
            query.WarehouseName = Text(query.WarehouseName, false);
            return _repository.QueryWarehouses(query);
        }

        public OperationResult<WarehouseDto> GetWarehouse(string warehouseCode)
        {
            WarehouseDto warehouse = _repository.GetWarehouse(Text(warehouseCode, true));
            return warehouse == null ? OperationResult<WarehouseDto>.Fail("仓库不存在。") : OperationResult<WarehouseDto>.Success(warehouse);
        }

        public OperationResult<WarehouseDto> CreateWarehouse(WarehouseCreateRequest request)
        {
            string error = ValidateWarehouse(request == null ? null : request.WarehouseCode, request == null ? null : request.WarehouseName,
                request == null ? null : request.WarehouseType, request == null ? null : request.Operator, true);
            if (error != null) return OperationResult<WarehouseDto>.Fail(error);
            if (request.Remark != null && request.Remark.Trim().Length > 1000) return OperationResult<WarehouseDto>.Fail("Remark长度不能超过1000。");
            request.WarehouseCode = Text(request.WarehouseCode, true);
            request.WarehouseName = Text(request.WarehouseName, false);
            request.WarehouseType = Text(request.WarehouseType, true);
            request.Remark = Text(request.Remark, false);
            request.Operator = Text(request.Operator, false);
            if (_repository.WarehouseExists(request.WarehouseCode)) return OperationResult<WarehouseDto>.Fail("WarehouseCode已存在。请编辑或启用现有仓库。");
            try { _repository.InsertWarehouse(request); }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627) { return OperationResult<WarehouseDto>.Fail("WarehouseCode已存在。"); }
            return OperationResult<WarehouseDto>.Success(_repository.GetWarehouse(request.WarehouseCode), "仓库创建成功。");
        }

        public OperationResult<WarehouseDto> UpdateWarehouse(string warehouseCode, WarehouseUpdateRequest request)
        {
            string code = Text(warehouseCode, true);
            string error = ValidateWarehouse(code, request == null ? null : request.WarehouseName,
                request == null ? null : request.WarehouseType, request == null ? null : request.Operator, false);
            if (error != null) return OperationResult<WarehouseDto>.Fail(error);
            if (request.Remark != null && request.Remark.Trim().Length > 1000) return OperationResult<WarehouseDto>.Fail("Remark长度不能超过1000。");
            if (_repository.GetWarehouse(code) == null) return OperationResult<WarehouseDto>.Fail("仓库不存在。");
            request.WarehouseName = Text(request.WarehouseName, false);
            request.WarehouseType = Text(request.WarehouseType, true);
            request.Remark = Text(request.Remark, false);
            request.Operator = Text(request.Operator, false);
            _repository.UpdateWarehouse(code, request);
            return OperationResult<WarehouseDto>.Success(_repository.GetWarehouse(code), "仓库修改成功。");
        }

        public OperationResult<WarehouseDto> SetWarehouseActive(string warehouseCode, bool isActive, MasterDataStatusRequest request)
        {
            string code = Text(warehouseCode, true);
            string operatorName = Text(request == null ? null : request.Operator, false);
            if (code == null) return OperationResult<WarehouseDto>.Fail("WarehouseCode不能为空。");
            if (!ValidOperator(operatorName)) return OperationResult<WarehouseDto>.Fail("Operator不能为空且长度不能超过50。");
            WarehouseDto warehouse = _repository.GetWarehouse(code);
            if (warehouse == null) return OperationResult<WarehouseDto>.Fail("仓库不存在。");
            if (!isActive)
            {
                if (_repository.HasCurrentHus(code)) return OperationResult<WarehouseDto>.Fail("仓库当前仍有HU，不允许停用。");
                if (_repository.HasActiveLocations(code)) return OperationResult<WarehouseDto>.Fail("仓库下仍有启用货位，不允许停用。");
                if (_repository.HasActiveMappings(code)) return OperationResult<WarehouseDto>.Fail("仓库下仍有启用厂别映射，不允许停用。");
            }
            _repository.SetWarehouseActive(code, isActive, operatorName);
            return OperationResult<WarehouseDto>.Success(_repository.GetWarehouse(code), isActive ? "仓库已启用。" : "仓库已停用。");
        }

        public List<WarehousePlantDto> QueryWarehousePlants(WarehousePlantQuery query)
        {
            query = query ?? new WarehousePlantQuery();
            query.WarehouseCode = Text(query.WarehouseCode, true);
            query.PlantCode = Text(query.PlantCode, true);
            return _repository.QueryWarehousePlants(query);
        }

        public OperationResult<WarehousePlantDto> GetWarehousePlant(string warehouseCode, string plantCode)
        {
            WarehousePlantDto mapping = _repository.GetWarehousePlant(Text(warehouseCode, true), Text(plantCode, true));
            return mapping == null ? OperationResult<WarehousePlantDto>.Fail("厂别映射不存在。") : OperationResult<WarehousePlantDto>.Success(mapping);
        }

        public OperationResult<WarehousePlantDto> CreateWarehousePlant(WarehousePlantCreateRequest request)
        {
            string error = ValidatePlant(request == null ? null : request.WarehouseCode, request == null ? null : request.PlantCode,
                request == null ? null : request.SapStorageLocation, request == null ? null : request.Operator);
            if (error != null) return OperationResult<WarehousePlantDto>.Fail(error);
            if (request.Remark != null && request.Remark.Trim().Length > 1000) return OperationResult<WarehousePlantDto>.Fail("Remark长度不能超过1000。");
            NormalizePlant(request);
            WarehouseDto warehouse = _repository.GetWarehouse(request.WarehouseCode);
            if (warehouse == null || !warehouse.IsActive) return OperationResult<WarehousePlantDto>.Fail("新增映射只能选择启用仓库。");
            if (_repository.WarehousePlantExists(request.WarehouseCode, request.PlantCode)) return OperationResult<WarehousePlantDto>.Fail("Warehouse + Plant映射已存在。请编辑或启用现有映射。");
            try { _repository.InsertWarehousePlant(request); }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627) { return OperationResult<WarehousePlantDto>.Fail("Warehouse + Plant映射已存在。"); }
            return OperationResult<WarehousePlantDto>.Success(_repository.GetWarehousePlant(request.WarehouseCode, request.PlantCode), "厂别映射创建成功。");
        }

        public OperationResult<WarehousePlantDto> UpdateWarehousePlant(string warehouseCode, string plantCode, WarehousePlantUpdateRequest request)
        {
            string code = Text(warehouseCode, true);
            string plant = Text(plantCode, true);
            string error = ValidatePlant(code, plant, request == null ? null : request.SapStorageLocation, request == null ? null : request.Operator);
            if (error != null) return OperationResult<WarehousePlantDto>.Fail(error);
            if (request.Remark != null && request.Remark.Trim().Length > 1000) return OperationResult<WarehousePlantDto>.Fail("Remark长度不能超过1000。");
            if (_repository.GetWarehousePlant(code, plant) == null) return OperationResult<WarehousePlantDto>.Fail("厂别映射不存在。");
            request.SapStorageLocation = Text(request.SapStorageLocation, true);
            request.Remark = Text(request.Remark, false);
            request.Operator = Text(request.Operator, false);
            _repository.UpdateWarehousePlant(code, plant, request);
            return OperationResult<WarehousePlantDto>.Success(_repository.GetWarehousePlant(code, plant), "厂别映射修改成功。");
        }

        public OperationResult<WarehousePlantDto> SetWarehousePlantActive(string warehouseCode, string plantCode, bool isActive, MasterDataStatusRequest request)
        {
            string code = Text(warehouseCode, true);
            string plant = Text(plantCode, true);
            string operatorName = Text(request == null ? null : request.Operator, false);
            if (code == null || plant == null) return OperationResult<WarehousePlantDto>.Fail("WarehouseCode和PlantCode不能为空。");
            if (!ValidOperator(operatorName)) return OperationResult<WarehousePlantDto>.Fail("Operator不能为空且长度不能超过50。");
            WarehousePlantDto mapping = _repository.GetWarehousePlant(code, plant);
            if (mapping == null) return OperationResult<WarehousePlantDto>.Fail("厂别映射不存在。");
            if (isActive)
            {
                WarehouseDto warehouse = _repository.GetWarehouse(code);
                if (warehouse == null || !warehouse.IsActive) return OperationResult<WarehousePlantDto>.Fail("停用仓库下的映射不能启用。");
            }
            _repository.SetWarehousePlantActive(code, plant, isActive);
            return OperationResult<WarehousePlantDto>.Success(_repository.GetWarehousePlant(code, plant), isActive ? "厂别映射已启用。" : "厂别映射已停用。");
        }

        public MasterDataOptionsDto GetOptions(string warehouseCode)
        {
            MasterDataOptionsDto result = new MasterDataOptionsDto();
            result.WarehouseTypes.Add(FinishedGoodsWarehouseType);
            result.LocationTypes.Add("DOUBLE_RACK");
            result.LocationTypes.Add("TRIPLE_TIER_REMAINDER_RACK");
            result.LocationTypes.Add("FLOOR_STORAGE");
            result.Warehouses = _repository.GetWarehouseOptions();
            string code = Text(warehouseCode, true);
            if (code != null) result.Zones = _repository.GetZoneOptions(code);
            return result;
        }

        private static string ValidateWarehouse(string code, string name, string type, string operatorName, bool requireCode)
        {
            string normalizedCode = Text(code, true);
            string normalizedName = Text(name, false);
            string normalizedType = Text(type, true);
            if (requireCode && normalizedCode == null) return "WarehouseCode不能为空。";
            if (normalizedCode != null && normalizedCode.Length > 30) return "WarehouseCode长度不能超过30。";
            if (normalizedName == null || normalizedName.Length > 200) return "WarehouseName不能为空且长度不能超过200。";
            if (normalizedType != FinishedGoodsWarehouseType) return "WarehouseType仅支持FINISHED_GOODS。";
            if (!ValidOperator(Text(operatorName, false))) return "Operator不能为空且长度不能超过50。";
            return null;
        }

        private static string ValidatePlant(string warehouseCode, string plantCode, string sapStorageLocation, string operatorName)
        {
            string code = Text(warehouseCode, true);
            string plant = Text(plantCode, true);
            string sap = Text(sapStorageLocation, true);
            if (code == null || code.Length > 30) return "WarehouseCode不能为空且长度不能超过30。";
            if (plant == null || plant.Length > 20) return "PlantCode不能为空且长度不能超过20。";
            if (sap != null && sap.Length > 20) return "SAPStorageLocation长度不能超过20。";
            if (!ValidOperator(Text(operatorName, false))) return "Operator不能为空且长度不能超过50。";
            return null;
        }

        private static void NormalizePlant(WarehousePlantCreateRequest request)
        {
            request.WarehouseCode = Text(request.WarehouseCode, true); request.PlantCode = Text(request.PlantCode, true);
            request.SapStorageLocation = Text(request.SapStorageLocation, true); request.Remark = Text(request.Remark, false); request.Operator = Text(request.Operator, false);
        }

        private static bool ValidOperator(string value) { return value != null && value.Length <= 50; }
        private static string Text(string value, bool upper)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string result = value.Trim();
            return upper ? result.ToUpperInvariant() : result;
        }
    }
}
