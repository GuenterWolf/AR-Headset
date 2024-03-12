#region " Using statements "

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

#endregion

public class RollaballRotate : MonoBehaviour
{
    #region " Variables definitions "

    float speed = 5;

    #endregion

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(new Vector3(0, 60, 0) * speed * Time.deltaTime, Space.World);
    }
}
