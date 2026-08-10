# 05 - API Test

## Postman 环境变量

```text
localUrl = https://localhost:44375
```

所有测试地址统一写：

```text
{{localUrl}}/api/xxx
```

## Health
```http
GET {{localUrl}}/api/health
GET {{localUrl}}/api/health/db
```

## Location
```http
GET {{localUrl}}/api/wms/locations
GET {{localUrl}}/api/wms/locations/FG01/H201
POST {{localUrl}}/api/wms/locations
PUT {{localUrl}}/api/wms/locations/FG01/H201
```

## HU
```http
POST {{localUrl}}/api/wms/hus
POST {{localUrl}}/api/wms/hus/attach
POST {{localUrl}}/api/wms/hus/I001/items
GET {{localUrl}}/api/wms/hus/P001/tree
```

## HU Location
```http
GET {{localUrl}}/api/wms/hus/B001/location
POST {{localUrl}}/api/wms/hus/B002/move
GET {{localUrl}}/api/wms/hus/B002/moves?top=20
```
