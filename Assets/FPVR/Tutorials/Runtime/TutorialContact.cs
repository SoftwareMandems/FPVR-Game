using UnityEngine;

namespace FPVR.Tutorials
{
    // Tutorial-only collision policy from MIS M6. The shared flight controller is untouched.
    [RequireComponent(typeof(DroneFlight))]
    public sealed class TutorialContact : MonoBehaviour
    {
        DroneFlight drone;
        bool airborne;
        public void ResetContact(bool inAir) { airborne = inAir; }
        void Awake() { drone = GetComponent<DroneFlight>(); }
        void FixedUpdate()
        {
            if (!drone.enabled || !drone.armed) return;
            var bounds = GetComponent<Collider>().bounds;
            // Ground and launch-pad tops are y=0 and y=.05. Detect even a small lift-off.
            if (bounds.min.y > .08f && drone.Body.linearVelocity.y > .01f) airborne = true;
        }
        void OnCollisionExit(Collision collision)
        {
            if (drone.enabled && drone.armed && IsGround(collision) && drone.Body.linearVelocity.y > .01f) airborne = true;
        }
        static bool IsGround(Collision collision) => collision.gameObject.name == "Ground" || collision.gameObject.name == "Launch pad";
        void OnCollisionEnter(Collision collision) { Contact(collision); }
        void OnCollisionStay(Collision collision) { Contact(collision); }
        void Contact(Collision collision)
        {
            if (!drone.enabled || drone.crashed) return;
            if (IsGround(collision) && !airborne) return;
            drone.crashed = true; drone.armed = false;
            drone.Body.linearVelocity = drone.Body.angularVelocity = Vector3.zero;
            drone.Body.isKinematic = true;
        }
    }
}
