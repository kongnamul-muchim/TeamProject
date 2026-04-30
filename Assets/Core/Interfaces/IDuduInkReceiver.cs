namespace HideAndInk.Core.Interfaces
{
    public interface IDuduInkReceiver
    {
        float CurrentInk { get; }
        float MaxInk { get; }
        bool CanReceiveInk { get; }
        float AddInk(float amount);
    }
}
