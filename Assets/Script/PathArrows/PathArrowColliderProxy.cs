/*
Summary:
PathArrowColliderProxy is attached to generated arrow colliders. It only stores
which PathArrow owns the collider, so GameManager can identify tapped arrows with
an explicit Physics2D raycast instead of Unity's built-in mouse callbacks.
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
}
