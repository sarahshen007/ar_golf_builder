using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionsMap
{
    public List<Action> actions;
    public int pointer;
    public int cutoff;

    public ActionsMap() {
        actions = new List<Action>();
        pointer = 0;
        cutoff = 0;
    }

    public void SetPointer(int val)
    {
        pointer = val;
    }

    public void IncPointer() {
        if (pointer >= cutoff || pointer >= actions.Count) {
            pointer = cutoff;
        } else {
            pointer++;
        }
    }

    public void DecPointer() {
        if (pointer <= 0) {
            pointer = 0;
        }
        else {
            pointer--;
        }
    }

    public void AddAction(Action new_action) {

        if (pointer < actions.Count) {
            actions[pointer] = new_action;
            cutoff = pointer + 1;
        } else {
            actions.Add(new_action);
            cutoff = actions.Count;
        }

        pointer++;
    }

    public Action GetLastAction() {
        if (pointer == 0) {
            return null;
        }

        return actions[pointer - 1];
    }

    public Action GetNextAction() {

        if (actions.Count == pointer || pointer == cutoff) {
            return null;
        }
        return actions[pointer];
    }
}
