using System;
using HideAndInk.Siyeon1;

namespace HideAndInk.Core.Interfaces
{
    public interface IDuduStateProvider
    {
        DuduState CurrentState { get; }
        bool IsMoving { get; }
        bool IsSmokeActive { get; }
        event Action<DuduState> StateChanged;
    }
}
