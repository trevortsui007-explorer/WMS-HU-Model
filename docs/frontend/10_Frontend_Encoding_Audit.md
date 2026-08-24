# Frontend Encoding Audit

## Existing Working File Samples

The audit read raw bytes, recorded the first three bytes, decoded with strict UTF-8, and only fell back to CP936 when strict UTF-8 failed. All 25 working samples started with `EF BB BF` and decoded as correct Unicode Chinese.

| File | Type | Encoding | BOM | Chinese OK |
|---|---|---|---|---|
| `Areas/LR_OrganizationModule/Views/User/Index.cshtml` | cshtml | UTF-8 | Yes | Yes |
| `Areas/LR_OrganizationModule/Views/User/Form.cshtml` | cshtml | UTF-8 | Yes | Yes |
| `Areas/LR_OrganizationModule/Views/Role/Index.cshtml` | cshtml | UTF-8 | Yes | Yes |
| `Areas/LR_OrganizationModule/Views/Department/Index.cshtml` | cshtml | UTF-8 | Yes | Yes |
| `Areas/LR_SystemModule/Views/Module/Index.cshtml` | cshtml | UTF-8 | Yes | Yes |
| `Areas/LR_SystemModule/Views/Log/Index.cshtml` | cshtml | UTF-8 | Yes | Yes |
| `Areas/LR_SystemModule/Views/DataItem/Index.cshtml` | cshtml | UTF-8 | Yes | Yes |
| `Areas/WMS/Views/WMS_MaterialStock/Index.cshtml` | cshtml | UTF-8 | Yes | Yes |
| `Areas/WMS/Views/HS_WarehouseLocation/Index.cshtml` | cshtml | UTF-8 | Yes | Yes |
| `Areas/WMS/Views/HS_WarehouseLocation/Form.cshtml` | cshtml | UTF-8 | Yes | Yes |
| `Areas/LR_OrganizationModule/Views/User/Index.js` | js | UTF-8 | Yes | Yes |
| `Areas/LR_OrganizationModule/Views/User/Form.js` | js | UTF-8 | Yes | Yes |
| `Areas/LR_OrganizationModule/Views/Role/Index.js` | js | UTF-8 | Yes | Yes |
| `Areas/LR_OrganizationModule/Views/Department/Index.js` | js | UTF-8 | Yes | Yes |
| `Areas/LR_SystemModule/Views/Module/Index.js` | js | UTF-8 | Yes | Yes |
| `Areas/LR_SystemModule/Views/Log/Index.js` | js | UTF-8 | Yes | Yes |
| `Areas/LR_SystemModule/Views/DataItem/Index.js` | js | UTF-8 | Yes | Yes |
| `Areas/WMS/Views/WMS_MaterialStock/Index.js` | js | UTF-8 | Yes | Yes |
| `Areas/WMS/Views/HS_WarehouseLocation/Index.js` | js | UTF-8 | Yes | Yes |
| `Areas/WMS/Views/HS_WarehouseLocation/Form.js` | js | UTF-8 | Yes | Yes |
| `Views/LR_Content/plugin/layout/lr-layout.css` | css | UTF-8 | Yes | Yes |
| `Views/LR_Content/style/lr-iframe-index.css` | css | UTF-8 | Yes | Yes |
| `Views/LR_Content/style/lr-iframe-form.css` | css | UTF-8 | Yes | Yes |
| `Views/LR_Content/plugin/grid/jfgrid.css` | css | UTF-8 | Yes | Yes |
| `Views/LR_Content/style/lr-common.css` | css | UTF-8 | Yes | Yes |

```text
WORKING_CSHtml_SAMPLE_COUNT=10
WORKING_JS_SAMPLE_COUNT=10
WORKING_CSS_SAMPLE_COUNT=5
UTF8_BOM_WORKING_FILE_COUNT=25
UTF8_NO_BOM_WORKING_FILE_COUNT=0
OTHER_ENCODING_WORKING_FILE_COUNT=0
```

## Foundation Files

Before conversion, every Foundation source was valid UTF-8 without BOM. Strict decoding showed correct Chinese text and no known mojibake markers. After V1.1, every `.cshtml`, `.js`, and `.css` file is UTF-8 with BOM. SHA-256 comparison of the UTF-8 payload excluding BOM confirmed the encoding-only conversion changed no text content.

| File | Encoding Before | Mojibake | Encoding After |
|---|---|---|---|
| `Foundation/Scripts/wms-core.js` | UTF-8 no BOM | No | UTF-8 BOM |
| `Foundation/Scripts/wms-api.js` | UTF-8 no BOM | No | UTF-8 BOM |
| `Foundation/Scripts/wms-adapters.js` | UTF-8 no BOM | No | UTF-8 BOM |
| `Foundation/Scripts/wms-components.js` | UTF-8 no BOM | No | UTF-8 BOM |
| `Foundation/Styles/wms-tokens.css` | UTF-8 no BOM | No | UTF-8 BOM |
| `Foundation/Styles/wms-components.css` | UTF-8 no BOM | No | UTF-8 BOM |
| `Foundation/Styles/wms-foundation-demo.css` | UTF-8 no BOM | No | UTF-8 BOM |
| `WMSFoundationDemo/Index.cshtml` | UTF-8 no BOM | No | UTF-8 BOM |
| `WMSFoundationDemo/Index.js` | UTF-8 no BOM | No | UTF-8 BOM |
| `WMSFoundationDemo/ModalDemo.cshtml` | UTF-8 no BOM | No | UTF-8 BOM |
| `WMSFoundationDemo/ModalDemo.js` | UTF-8 no BOM | No | UTF-8 BOM |

```text
FOUNDATION_MOJIBAKE_FILE_COUNT_BEFORE=0
FOUNDATION_MOJIBAKE_FILE_COUNT_AFTER=0
```

## Web.config

- Root `Web.config` and `Views/Web.config` do not contain an explicit `<globalization requestEncoding="..." responseEncoding="..." fileEncoding="..." />` element.
- Several unrelated export endpoints set per-response GB2312/UTF-8 values, but the Foundation controller does not set `Response.ContentEncoding` or `Response.Charset` and does not use those export paths.
- Decision value: `WEB_CONFIG_ENCODING=NOT_EXPLICITLY_CONFIGURED`.

## HTML Charset

`Views/Shared/_Admin.cshtml`, `_Index.cshtml`, and `_Form.cshtml` each declare:

```html
<meta http-equiv="content-type" content="text/html; charset=UTF-8">
```

Decision value: `HTML_CHARSET=UTF-8`.

## Root Cause

The visible corruption was not stored mojibake: all Foundation Chinese decoded correctly as strict UTF-8. The mismatch was that the new files had no BOM while all 25 sampled, known-working Chinese files use UTF-8 BOM. In this legacy Windows / Visual Studio / Razor toolchain, BOM is the consistent byte-level encoding signal. Adding BOM after preserving the decoded Unicode payload addresses the evidence-backed mismatch.

## Final Encoding Standard

```text
FINAL_WMS_SOURCE_ENCODING=UTF-8_WITH_BOM
```

All new WMS `.cshtml`, `.js`, and `.css` files containing or potentially receiving Chinese text must be saved as UTF-8 with BOM. Do not attempt GBK round-trips for already valid UTF-8 text.
