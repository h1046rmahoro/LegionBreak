using UnityEngine;

namespace LegionBreak.Application.Movement
{
    public interface IPlayerMoveUseCase
    {
        void Execute(Vector2 inputDirection, float deltaTime);
    }
}
