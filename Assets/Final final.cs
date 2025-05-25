using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;

public class Dedos6 : MonoBehaviour
{
    SerialPort stream = new SerialPort("COM5", 115200);
    public string strReceived;
    public string[] strData = new string[24];
    public float qx1, qx2, qx3, qx4, qx5;
    public float qw6, qx6, qy6, qz6;

    public Transform b_r_index1, b_r_index2, b_r_index3;
    public Transform b_r_middle1, b_r_middle2, b_r_middle3;
    public Transform b_r_ring1, b_r_ring2, b_r_ring3;
    public Transform b_r_pinky1, b_r_pinky2, b_r_pinky3;
    public Transform b_r_thumb1, b_r_thumb2, b_r_thumb3;
    public Transform b_r_wrist;

    private float[] minAngles = new float[5];
    private float[] maxAngles = new float[5];

    void Start()
    {
        stream.Open();
        Debug.Log("Conexión serial abierta.");

        for (int i = 0; i < 5; i++)
        {
            minAngles[i] = float.MaxValue;
            maxAngles[i] = float.MinValue;
        }
    }

    void Update()
    {
        if (stream.IsOpen)
        {
            try
            {
                strReceived = stream.ReadLine();
                strData = strReceived.Split(',');

                if (strData.Length >= 24)
                {
                    if (float.TryParse(strData[1].Trim(), out qx1) &&
                        float.TryParse(strData[5].Trim(), out qx2) &&
                        float.TryParse(strData[9].Trim(), out qx3) &&
                        float.TryParse(strData[13].Trim(), out qx4) &&
                        float.TryParse(strData[17].Trim(), out qx5) &&
                        float.TryParse(strData[20].Trim(), out qw6) &&
                        float.TryParse(strData[21].Trim(), out qx6) &&
                        float.TryParse(strData[22].Trim(), out qy6) &&
                        float.TryParse(strData[23].Trim(), out qz6))
                    {
                        float[] angles = { qx1, qx2, qx3, qx4, qx5 };

                        for (int i = 0; i < 5; i++)
                        {
                            float angleInDegrees = angles[i] * Mathf.Rad2Deg;
                            if (angleInDegrees < minAngles[i]) minAngles[i] = angleInDegrees;
                            if (angleInDegrees > maxAngles[i]) maxAngles[i] = angleInDegrees;
                        }

                        // Imprimir los rangos de movimiento en grados
                        Debug.Log($"Índice: {maxAngles[0] - minAngles[0]}°");
                        Debug.Log($"Medio: {maxAngles[1] - minAngles[1]}°");
                        Debug.Log($"Anular: {maxAngles[2] - minAngles[2]}°");
                        Debug.Log($"Meñique: {maxAngles[3] - minAngles[3]}°");
                        Debug.Log($"Pulgar: {maxAngles[4] - minAngles[4]}°");

                        // Aplicar rotaciones
                        b_r_index3.localRotation = Quaternion.Euler(0, 0, qx1 * 350.0f / Mathf.PI);
                        b_r_index2.localRotation = Quaternion.Euler(0, 0, qx1 * 300.0f / Mathf.PI);
                        b_r_index1.localRotation = Quaternion.Euler(0, 0, qx1 * 250.0f / Mathf.PI);

                        b_r_middle3.localRotation = Quaternion.Euler(0, 0, qx2 * 350.0f / Mathf.PI);
                        b_r_middle2.localRotation = Quaternion.Euler(0, 0, qx2 * 300.0f / Mathf.PI);
                        b_r_middle1.localRotation = Quaternion.Euler(0, 0, qx2 * 250.0f / Mathf.PI);

                        b_r_ring3.localRotation = Quaternion.Euler(0, 0, qx3 * 350.0f / Mathf.PI);
                        b_r_ring2.localRotation = Quaternion.Euler(0, 0, qx3 * 300.0f / Mathf.PI);
                        b_r_ring1.localRotation = Quaternion.Euler(0, 0, qx3 * 250.0f / Mathf.PI);

                        b_r_pinky3.localRotation = Quaternion.Euler(0, 0, qx4 * 350.0f / Mathf.PI);
                        b_r_pinky2.localRotation = Quaternion.Euler(0, 0, qx4 * 300.0f / Mathf.PI);
                        b_r_pinky1.localRotation = Quaternion.Euler(0, 0, qx4 * 250.0f / Mathf.PI);

                        b_r_thumb3.localRotation = Quaternion.Euler(0, 0, qx5 * 350.0f / Mathf.PI);
                        b_r_thumb2.localRotation = Quaternion.Euler(0, 0, qx5 * 300.0f / Mathf.PI);
                        b_r_thumb1.localRotation = Quaternion.Euler(0, 0, qx5 * 250.0f / Mathf.PI);

                        b_r_wrist.localRotation = new Quaternion(qx6, qy6, qz6, qw6);
                    }
                    else
                    {
                        Debug.LogError("Error al convertir datos de los sensores.");
                    }
                }
                else
                {
                    Debug.LogError("Datos insuficientes recibidos. strData.Length: " + strData.Length);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error leyendo datos del puerto serial: " + e.Message);
            }
        }
    }
}


