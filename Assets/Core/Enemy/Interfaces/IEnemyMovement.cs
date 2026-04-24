using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Enemy.Interfaces
{
    /// <summary>
    /// Ground 경계 정보
    /// </summary>
    public struct GroundBounds
    {
        public float MinX;
        public float MaxX;
        public float MinZ;
        public float MaxZ;

        public GroundBounds(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        /// <summary>
        /// XZ 위치를 Ground 범위 내로 제한
        /// </summary>
        public Vector3 ClampXZ(Vector3 position)
        {
            return new Vector3(
                Mathf.Clamp(position.x, MinX, MaxX),
                position.y,
                Mathf.Clamp(position.z, MinZ, MaxZ)
            );
        }

        /// <summary>
        /// X 위치를 Ground 범위 내로 제한
        /// </summary>
        public float ClampX(float x)
        {
            return Mathf.Clamp(x, MinX, MaxX);
        }

        /// <summary>
        /// Z 위치를 Ground 범위 내로 제한
        /// </summary>
        public float ClampZ(float z)
        {
            return Mathf.Clamp(z, MinZ, MaxZ);
        }
    }

    /// <summary>
    /// Enemy 이동 시스템 인터페이스
    /// 3D 환경 (X-Z 평면 이동, Y축 고정)
    /// </summary>
    public interface IEnemyMovement
    {
        /// <summary>
        /// 현재 속도 벡터 (X-Z 평면)
        /// </summary>
        Vector3 Velocity { get; }

        /// <summary>
        /// 현재 이동 방향 (2D 기준: Left/Right/Up/Down)
        /// </summary>
        MoveDirection Direction { get; }

        /// <summary>
        /// 이동 중인지 여부
        /// </summary>
        bool IsMoving { get; }

        /// <summary>
        /// 이동 속도 (인스펙터 설정 가능)
        /// </summary>
        float Speed { get; set; }

        /// <summary>
        /// 최대 속도 설정 (돌진 등 임시 속도 증가용)
        /// </summary>
        void SetMaxSpeed(float maxSpeed);

        /// <summary>
        /// 목표 위치로 이동 (X-Z 평면)
        /// </summary>
        /// <param name="targetPosition">목표 위치 (월드 좌표, X-Z 사용)</param>
        void MoveTo(Vector3 targetPosition);

        /// <summary>
        /// 이동 즉시 중지
        /// </summary>
        void Stop();

        /// <summary>
        /// 상태 업데이트 (매 프레임 호출)
        /// </summary>
        /// <param name="deltaTime">경과 시간</param>
        void Update(float deltaTime);

        /// <summary>
        /// 지정 위치가 Ground 위에 있는지 확인
        /// </summary>
        bool IsPositionOnGround(Vector3 position);

        /// <summary>
        /// 이동 방향 앞쪽에 Ground가 있는지 확인
        /// </summary>
        bool IsGroundAhead();

        /// <summary>
        /// Ground 경계 스캔 (시작 시 호출)
        /// </summary>
        GroundBounds ScanGroundBounds(float maxScanDistance = 50f, float scanStep = 1f);
    }
}
