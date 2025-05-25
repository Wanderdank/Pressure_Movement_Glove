using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class FinalFinal2 : MonoBehaviour
{
    // --- Serial ---
    SerialPort stream = new SerialPort("COM5", 115200);
    Thread serialThread;
    bool keepReading = false;
    string latestLine = "";

    [Header("DEBUG: muestra el último paquete serial")]
    public string debugLine;

    // --- Datos recibidos ---
    public string[] strData = new string[24];
    float qx1, qx2, qx3, qx4, qx5;
    float qw6, qx6, qy6, qz6;

    // --- Transforms de los dedos ---
    public Transform b_r_index1, b_r_index2, b_r_index3;
    public Transform b_r_middle1, b_r_middle2, b_r_middle3;
    public Transform b_r_ring1, b_r_ring2, b_r_ring3;
    public Transform b_r_pinky1, b_r_pinky2, b_r_pinky3;
    public Transform b_r_thumb1, b_r_thumb2, b_r_thumb3;
    public Transform b_r_wrist;

    // --- Calibración y rangos ---
    private float[] minAngles = new float[5];
    private float[] maxAngles = new float[5];
    private float[] initialAngles = new float[5];

    // --- Logs amortiguados ---
    float logTimer = 0f;

    // --- Factores precalculados ---
    float f1, f2, f3;

    void Start()
    {
        // Precomputar constantes
        f1 = 350f / Mathf.PI;
        f2 = 300f / Mathf.PI;
        f3 = 250f / Mathf.PI;

        // Inicializar rangos/calibración
        for (int i = 0; i < 5; i++)
        {
            minAngles[i] = float.MaxValue;
            maxAngles[i] = float.MinValue;
            initialAngles[i] = 0f;
        }

        // Abrir puerto
        stream.ReadTimeout = 100;      // 100 ms de espera
        stream.Open();

        // Iniciar hilo
        keepReading = true;
        serialThread = new Thread(SerialLoop) { IsBackground = true };
        serialThread.Start();
        Debug.Log("Serial thread started.");
    }

    void SerialLoop()
    {
        while (keepReading)
        {
            try
            {
                string line = stream.ReadLine();  // bloqueante con timeout
                if (string.IsNullOrWhiteSpace(line)) 
                    continue;

                // Validar mínimo 24 campos
                var parts = line.Split(',');
                if (parts.Length >= 24)
                {
                    lock (this)
                    {
                        latestLine = line;
                    }
                }
            }
            catch (System.TimeoutException)
            {
                // no hay datos → seguimos intentando
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Serial read error (fatal): {e.Message}");
                // si realmente quieres seguir intentando, no hagas keepReading=false
                keepReading = false;
            }
        }
    }

    void Update()
    {
        // Traer última línea
        string data;
        lock (this)
        {
            data = latestLine;
        }

        if (string.IsNullOrEmpty(data))
            return;

        // Pinta en Inspector para debug
        debugLine = data;

        // Parsear
        strData = data.Split(',');
        if (strData.Length < 24)
            return;

        bool ok =
            float.TryParse(strData[1], out qx1) &&
            float.TryParse(strData[5], out qx2) &&
            float.TryParse(strData[9], out qx3) &&
            float.TryParse(strData[13], out qx4) &&
            float.TryParse(strData[17], out qx5) &&
            float.TryParse(strData[20], out qw6) &&
            float.TryParse(strData[21], out qx6) &&
            float.TryParse(strData[22], out qy6) &&
            float.TryParse(strData[23], out qz6);

        if (!ok)
            return;

        // Actualizar rangos
        float[] angles = { qx1, qx2, qx3, qx4, qx5 };
        for (int i = 0; i < 5; i++)
        {
            float deg = (angles[i] - initialAngles[i]) * Mathf.Rad2Deg;
            if (deg < minAngles[i]) minAngles[i] = deg;
            if (deg > maxAngles[i]) maxAngles[i] = deg;
        }

        // Logs amortiguados
        logTimer += Time.deltaTime;
        if (logTimer >= 1f)
        {
            Debug.Log($"Rangos ° — Índice: {maxAngles[0]-minAngles[0]:F1}, Medio: {maxAngles[1]-minAngles[1]:F1}, Anular: {maxAngles[2]-minAngles[2]:F1}, Meñique: {maxAngles[3]-minAngles[3]:F1}, Pulgar: {maxAngles[4]-minAngles[4]:F1}");
            logTimer = 0f;
        }

        // Aplicar rotaciones
        ApplyFinger(b_r_index1, b_r_index2, b_r_index3, qx1, 0);
        ApplyFinger(b_r_middle1, b_r_middle2, b_r_middle3, qx2, 1);
        ApplyFinger(b_r_ring1, b_r_ring2, b_r_ring3, qx3, 2);
        ApplyFinger(b_r_pinky1, b_r_pinky2, b_r_pinky3, qx4, 3);
        ApplyFinger(b_r_thumb1, b_r_thumb2, b_r_thumb3, qx5, 4);

        // Muñeca
        b_r_wrist.localRotation = new Quaternion(qx6, qy6, qz6, qw6);
    }

    void ApplyFinger(Transform joint1, Transform joint2, Transform joint3, float raw, int idx)
    {
        float delta = raw - initialAngles[idx];
        joint3.localRotation = Quaternion.Euler(0, 0, delta * f1);
        joint2.localRotation = Quaternion.Euler(0, 0, delta * f2);
        joint1.localRotation = Quaternion.Euler(0, 0, delta * f3);
    }

    public void Calibrate()
    {
        initialAngles[0] = qx1;
        initialAngles[1] = qx2;
        initialAngles[2] = qx3;
        initialAngles[3] = qx4;
        initialAngles[4] = qx5;
        for (int i = 0; i < 5; i++)
        {
            minAngles[i] = float.MaxValue;
            maxAngles[i] = float.MinValue;
        }
        Debug.Log("Calibración completada.");
    }

    void OnDestroy()
    {
        keepReading = false;
        if (serialThread != null && serialThread.IsAlive)
            serialThread.Join(100);
        if (stream.IsOpen)
            stream.Close();
    }
}
