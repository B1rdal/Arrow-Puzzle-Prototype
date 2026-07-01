using UnityEngine;

// Small click-forwarder placed on generated colliders and the arrow head.
public class PathArrowColliderProxy : MonoBehaviour
{
    private PathArrow owner;

    public PathArrow Owner => owner;

    public void Initialize(PathArrow pathArrow)
    {
        owner = pathArrow;
    }

    private void OnMouseDown()
    {
        if (owner != null)
        {
            owner.HandlePressStarted();
        }
    }
}
