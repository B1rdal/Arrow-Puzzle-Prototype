/*
Summary:
PathArrowColliderProxy is attached to generated colliders on an arrow. Unity sends
mouse/touch press events to this proxy, and the proxy forwards them to the owning
PathArrow.
*/

using UnityEngine;

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
