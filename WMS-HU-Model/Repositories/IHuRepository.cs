using System.Collections.Generic;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Repositories
{
    /// <summary>
    /// 处理单元 (HU) 数据持久化仓储接口
    /// </summary>
    public interface IHuRepository
    {
        #region 基础查询 (Basic Queries)

        /// <summary>
        /// 根据 HU 编码获取 HU 详细信息
        /// </summary>
        HuDto GetByCode(string huCode);

        /// <summary>
        /// 根据 HU 编码获取 HU 内部主键 ID
        /// </summary>
        long? GetHuId(string huCode);

        /// <summary>
        /// 获取 HU 的类型 (例如：托盘/外箱/内盒)
        /// </summary>
        string GetHuType(long huId);

        /// <summary>
        /// 获取 HU 关联的工厂编码
        /// </summary>
        string GetPlantCode(long huId);

        /// <summary>
        /// 根据包装规范编码获取包装规范 ID
        /// </summary>
        long? GetPackageSpecId(string packageSpecCode);

        /// <summary>
        /// 获取当前子 HU 活跃的父级 HU 信息
        /// </summary>
        HuDto GetActiveParent(long childHuId);

        #endregion

        #region 校验与逻辑约束 (Validation & Constraints)

        /// <summary>
        /// 检查子 HU 是否已经绑定了活跃的父 HU
        /// </summary>
        bool HasActiveParent(long childHuId);

        /// <summary>
        /// 检查将 childHuId 挂载到 parentHuId 是否会产生循环引用 (如：把父节点挂载到子节点下)
        /// </summary>
        bool WouldCreateCycle(long parentHuId, long childHuId);

        #endregion

        #region 数据写入与变更 (Data Operations)

        /// <summary>
        /// 创建一个新的 HU 处理单元
        /// </summary>
        long Create(CreateHuRequest request, long? packageSpecId, string operationNo);

        /// <summary>
        /// 将子 HU 挂载/绑定到父 HU 节点下
        /// </summary>
        void Attach(long parentHuId, long childHuId, AttachHuRequest request, string operationNo);

        /// <summary>
        /// 为指定的 HU 添加物料明细/装箱明细
        /// </summary>
        long AddItem(long huId, AddHuItemRequest request, string operationNo);

        #endregion

        #region 层级与树状结构查询 (Tree & Item Queries)

        /// <summary>
        /// 获取以 rootHuId 为根节点的展平树状结构数据
        /// </summary>
        List<HuTreeFlatRow> GetTree(long rootHuId);

        /// <summary>
        /// 获取指定 HU 下包含的所有物料明细
        /// </summary>
        List<HuItemDto> GetItems(long huId);

        #endregion
    }
}