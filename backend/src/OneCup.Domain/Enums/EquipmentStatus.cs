namespace OneCup.Domain.Enums;

/// <summary>
/// 设备实例的运行状态。
/// </summary>
public enum EquipmentStatus
{
    /// <summary>运行中</summary>
    Running = 0,
    /// <summary>停机</summary>
    Stopped = 1,
    /// <summary>维修中</summary>
    Maintenance = 2,
}
