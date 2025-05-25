using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;

public class SerialTest : MonoBehaviour
{
    SerialPort stream = new SerialPort("COM3", 115200);
    public string strReceived;
    public string[] strData = new string[4];
    public float qx;

    // Falanges del dedo
    public Transform b_r_index1; // Falange menos sensible
    public Transform b_r_index2; // Falange intermedia
    public Transform b_r_index3; // Falange más sensible

    void Start()
    {
        stream.Open(); // Abrir la conexión serial
    }

    // Update se llama una vez por frame
    void Update()
    {
        if (stream.IsOpen)
        {
            try
            {
                strReceived = stream.ReadLine(); // Leer la información
                strData = strReceived.Split(',');

                if (strData.Length > 1 && float.TryParse(strData[1], out qx)) // Asegurarse de que qx está listo
                {
                    Debug.Log("qx recibido: " + qx); // Mensaje de depuración

                    // Aplicar rotaciones con diferentes sensibilidades a cada falange usando solo qx
                    b_r_index3.localRotation = Quaternion.Euler(0, 0, qx * 270.0f / Mathf.PI); // Falange más sensible
                    b_r_index2.localRotation = Quaternion.Euler(0, 0, qx * 150.0f / Mathf.PI); // Falange intermedia
                    b_r_index1.localRotation = Quaternion.Euler(0, 0, qx * 132.0f / Mathf.PI); // Falange menos sensible
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error leyendo datos del puerto serial: " + e.Message);
            }
        }
    }
}
