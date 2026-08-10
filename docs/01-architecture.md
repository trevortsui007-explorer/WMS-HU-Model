# 01 - Architecture

## 项目分层

```text
Postman / PDA / Web
        ↓
Nancy Module
        ↓
Service
        ↓
Repository
        ↓
SQL Server
```

## 项目边界

### WMS
Web/API 宿主层：
- Nancy Module
- Route / Query / Body 参数转换
- API 结果输出

### WMS-HU-Model
核心业务层：
- Models
- Services
- Repositories
- Infrastructure
- SQL scripts

## 约束
- Nancy Module 不直接写 SQL
- Service 层以下禁止 dynamic
- Route / Query 参数必须先转 object，再显式 Convert 为强类型
- 多表业务动作必须使用事务
