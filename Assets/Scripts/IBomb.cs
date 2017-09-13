using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// an interface for bombs that explode, arm, and have countdowns
public interface IBomb {

    /// detonates the explosive and ruins your whole day
    void Detonate();

    /// attempts to disarm the bomb, may require a secret key
    void Disarm(string key="");

    /// begins the process of arming the bomb
    void Arm();

}
