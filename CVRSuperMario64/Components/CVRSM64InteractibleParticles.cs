using MelonLoader;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class CVRSM64InteractableParticles : MonoBehaviour {

    public enum ParticleType {
        GoldCoin,
        BlueCoin,
        RedCoin,
    }

    [SerializeField] private ParticleType particleType = ParticleType.GoldCoin;

    private const string MarioParticleTargetName = "[CVRSM64InteractableParticlesTarget]";

    // Internal
    [NonSerialized] private ParticleSystem _particleSystem;
    [NonSerialized] private readonly List<ParticleCollisionEvent> _collisionEvents = new();

    private void Start() {
        _particleSystem = GetComponent<ParticleSystem>();

        if (_particleSystem == null) {
            MelonLogger.Error($"[{nameof(CVRSM64InteractableParticles)}] This component requires to be next to a particle system!");
            Destroy(this);
            return;
        }
    }

    private void OnParticleCollision(GameObject other) {
        if (other.name != MarioParticleTargetName) return;
        var marioTarget = other.GetComponentInParent<CVRSM64Mario>();
        if (marioTarget == null || !marioTarget.IsMine()) return;

        var numCollisionEvents = _particleSystem.GetCollisionEvents(other, _collisionEvents);
        for (var i = 0; i < numCollisionEvents; i++) {
            marioTarget.PickupCoin(particleType);
        }
    }
}
