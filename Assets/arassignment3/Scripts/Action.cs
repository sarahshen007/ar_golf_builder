using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Action
{
    public GameObject manip;
    public Vector3 og_position;
    public Quaternion og_rotation;
    public Vector3 new_position;
    public Quaternion new_rotation;
    public string type;
    public List<GameObject> affected;

    public Action(GameObject m, Vector3 og_p, Quaternion og_r, Vector3 new_p, Quaternion new_r, string t, List<GameObject> a) {
        manip = m;
        og_position = og_p;
        og_rotation = og_r;
        new_position = new_p;
        new_rotation = new_r;
        type = t;
        affected = a;
    }
}
