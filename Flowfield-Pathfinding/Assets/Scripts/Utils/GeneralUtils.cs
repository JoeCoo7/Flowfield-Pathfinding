using System;
using UnityEngine;


//-----------------------------------------------------------------------------
public static class GeneralUtils
{
    //-----------------------------------------------------------------------------
    public static T InstantiateAssetFromResource<T>(string _name, string _newName = "") where T : ScriptableObject
    {
        T objectData = Resources.Load(_name) as T;
        if (objectData == null)
            throw new ApplicationException(_name + " scriptable object not found");

        if (_newName != "")
            objectData.name = _newName;
        else
            objectData.name = _name.Substring(_name.LastIndexOf("/", StringComparison.Ordinal) + 1);

        return objectData;
    }

    //-----------------------------------------------------------------------------
    public static bool IsPointOnScreen(Camera _camera, Vector3 _position)
    {
        var planes = GeometryUtility.CalculateFrustumPlanes(_camera);
        foreach(var plane in planes)
        {
            if(plane.GetDistanceToPoint(_position) < 0)
                return false;
        }
        return true;
    }
}
