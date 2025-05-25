using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class MCP2 : MonoBehaviour
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

    // --- Calibración ---
    private float[] initialAngles = new float[5];
    private Quaternion initialWristRotation;
    private bool isCalibrated = false;

    // --- Transforms de la mano ---
    public Transform b_r_index1, b_r_index2, b_r_index3;
    public Transform b_r_middle1, b_r_middle2, b_r_middle3;
    public Transform b_r_ring1, b_r_ring2, b_r_ring3;
    public Transform b_r_pinky1, b_r_pinky2, b_r_pinky3;
    public Transform b_r_thumb1, b_r_thumb2, b_r_thumb3;
    public Transform b_r_wrist;

    // --- Factores de sensibilidad ---
    float smallJointFactor;
    float mainJointFactor;

    // --- Logs amortiguados ---
    float logTimer = 0f;

    // --- Lista para los datos filtrados (1/s) ---
    private List<float[]> loggedAngleData = new List<float[]>();

    void Start()
    {
        smallJointFactor = 1f / Mathf.PI;
        mainJointFactor = 360f / Mathf.PI;

        for (int i = 0; i < 5; i++)
            initialAngles[i] = 0f;
        initialWristRotation = Quaternion.identity;

        stream.ReadTimeout = 100;
        try
        {
            stream.Open();
            Debug.Log("MCP2: Puerto COM5 abierto.");
        }
        catch
        {
            Debug.LogError("MCP2: Error abriendo COM5.");
            enabled = false;
            return;
        }

        keepReading = true;
        serialThread = new Thread(SerialLoop) { IsBackground = true };
        serialThread.Start();
    }

    void SerialLoop()
    {
        while (keepReading)
        {
            try
            {
                string line = stream.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                if (parts.Length >= 24)
                {
                    lock (this) { latestLine = line; }
                }
            }
            catch (System.TimeoutException) { }
            catch (System.Exception e)
            {
                Debug.LogError("MCP2 SerialLoop error: " + e.Message);
                keepReading = false;
            }
        }
    }

    void Update()
    {
        string data;
        lock (this) { data = latestLine; }
        if (!string.IsNullOrEmpty(data))
        {
            debugLine = data;
            ParseAndApply(data);
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            Debug.Log("Guardando historial filtrado de ángulos...");
            SaveAnglesToFiles();
        }
    }

    void ParseAndApply(string line)
    {
        strData = line.Split(',');
        if (strData.Length < 24 || !isCalibrated) return;

        if (!(float.TryParse(strData[1], out qx1) &&
              float.TryParse(strData[5], out qx2) &&
              float.TryParse(strData[9], out qx3) &&
              float.TryParse(strData[13], out qx4) &&
              float.TryParse(strData[17], out qx5) &&
              float.TryParse(strData[20], out qw6) &&
              float.TryParse(strData[21], out qx6) &&
              float.TryParse(strData[22], out qy6) &&
              float.TryParse(strData[23], out qz6)))
            return;

        // Ángulos relativos (radianes)
        float[] raw = { qx1, qx2, qx3, qx4, qx5 };
        for (int i = 0; i < 5; i++) raw[i] -= initialAngles[i];

        // Guardar ángulos actuales en grados
        float[] currentAnglesDegrees = {
            raw[0] * Mathf.Rad2Deg * 2.3f,
            raw[1] * Mathf.Rad2Deg * 2.3f,
            raw[2] * Mathf.Rad2Deg * 2.3f,
            raw[3] * Mathf.Rad2Deg * 2.3f,
            raw[4] * Mathf.Rad2Deg * 2.3f,
            GetSignedWristAngle()
        };

        // Aplicar a la mano virtual
        ApplyFinger(b_r_index1, b_r_index2, b_r_index3, raw[0]);
        ApplyFinger(b_r_middle1, b_r_middle2, b_r_middle3, raw[1]);
        ApplyFinger(b_r_ring1, b_r_ring2, b_r_ring3, raw[2]);
        ApplyFinger(b_r_pinky1, b_r_pinky2, b_r_pinky3, raw[3]);
        ApplyFinger(b_r_thumb1, b_r_thumb2, b_r_thumb3, raw[4]);

        Quaternion currentWrist = new Quaternion(qx6, qy6, qz6, qw6);
        b_r_wrist.localRotation = Quaternion.Inverse(initialWristRotation) * currentWrist;

        // Logs amortiguados y guardar solo estos datos
        logTimer += Time.deltaTime;
        if (logTimer >= 1f)
        {
            Debug.Log($"Í:{currentAnglesDegrees[0]:F1}°, M:{currentAnglesDegrees[1]:F1}°, A:{currentAnglesDegrees[2]:F1}°, " +
                      $"Me:{currentAnglesDegrees[3]:F1}°, P:{currentAnglesDegrees[4]:F1}°, Wz:{currentAnglesDegrees[5]:F1}°");

            loggedAngleData.Add(currentAnglesDegrees);
            logTimer = 0f;
        }
    }

    void ApplyFinger(Transform j1, Transform j2, Transform j3, float angleRad)
    {
        float mainRot = angleRad * mainJointFactor;
        float smallRot = angleRad * smallJointFactor;
        j1.localRotation = Quaternion.Euler(0, 0, mainRot);
        j2.localRotation = Quaternion.Euler(0, 0, smallRot);
        j3.localRotation = Quaternion.Euler(0, 0, smallRot);
    }

    float GetSignedWristAngle()
    {
        float z = b_r_wrist.localEulerAngles.z;
        if (z > 180f) z -= 360f;
        return z;
    }

    public void Calibrate()
    {
        initialAngles[0] = qx1;
        initialAngles[1] = qx2;
        initialAngles[2] = qx3;
        initialAngles[3] = qx4;
        initialAngles[4] = qx5;
        initialWristRotation = new Quaternion(qx6, qy6, qz6, qw6);
        isCalibrated = true;
        Debug.Log("MCP2 calibrado con sensibilidad MCP.");
    }

    void SaveAnglesToFiles()
    {
        string txtPath = Path.Combine(Application.dataPath, "AngulosMCP2_filtrados.txt");
        using (StreamWriter writer = new StreamWriter(txtPath))
        {
            foreach (var angles in loggedAngleData)
                writer.WriteLine(string.Join(",", angles));
        }
        Debug.Log("Historial de ángulos (filtrados) guardado en " + txtPath);
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
