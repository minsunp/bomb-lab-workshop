using System.Collections;
using System.Collections.Generic;
using DateTime=System.DateTime;
using UnityEngine;

public class Bomb : MonoBehaviour, IBomb {
    bool isArmed;

    [SerializeField] protected GameObject explosion;
    [SerializeField] protected GameObject success;

    async void Start() { await 10; Arm(); }

    public async void Arm() { await 1; isArmed = true; }

    void Update() { if (DateTime.Now>new DateTime(2017, 9, 13, 11, 30, 0)) Detonate(); }

    void OnCollisionEnter(Collision c) { if (c.rigidbody.gameObject.tag=="Key") Detonate(); }

    public void Disarm(string key="") { if (!isArmed) { Detonate(); } Instantiate(success); }

    public void Detonate() { Instantiate(explosion); gameObject.SetActive(false); }

}
