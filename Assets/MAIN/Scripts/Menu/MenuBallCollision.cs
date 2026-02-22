using UnityEngine;

// put this on the player ball so it can forward collision events to the menu controller
public class MenuBallCollision : MonoBehaviour
{
    [HideInInspector] public MainMenuSmashController controller;

    void OnCollisionEnter(Collision collision)
    {
        if (controller != null)
            controller.NotifyCollision(collision.gameObject, transform.position);
    }

    void OnTriggerEnter(Collider other)
    {
        if (controller != null)
            controller.NotifyCollision(other.gameObject, transform.position);
    }
}
