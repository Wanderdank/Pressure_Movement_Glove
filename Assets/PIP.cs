using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using UnityEngine;

public class PIP : MonoBehaviour
{
    [Header("Serial Settings")]
    [SerializeField] private string portName = "COM5";
    [SerializeField] private int baudRate = 115200;
    [SerializeField] private int readTimeout = 50;

    private SerialPort stream;
    private readonly ConcurrentQueue<string> lineQueue = new ConcurrentQueue<string>();

    [Header("Raw Data")]
    private string strReceived;
    private string[] strData = new string[24];

    [Header("Sensor Values")]
    public float qx1, qx2, qx3, qx4, qx5;
    public float qw6, qx6, qy6, qz6;

    [Header("Initial Calibration")]
    private float initialQx1, initialQx2, initialQx3, initialQx4, initialQx5;
    private Quaternion initialWristRotation;
    private bool isCalibrated = false;

    [Header("Finger Transforms")]
    public Transform b_r_index1, b_r_index2, b_r_index3;
    public Transform b_r_middle1, b_r_middle2, b_r_middle3;
    public Transform b_r_ring1, b_r_ring2, b_r_ring3;
    public Transform b_r_pinky1, b_r_pinky2, b_r_pinky3;
    public Transform b_r_thumb1, b_r_thumb2, b_r_thumb3;
    [Header("Wrist Transform")]
    public Transform b_r_wrist;

    [Header("Logging")]
    [SerializeField] private string logFileName = "captura_datos.txt";
    private string logFilePath;

    void Start()
    {
        // Inicializa ruta de log
        logFilePath = Path.Combine(Application.dataPath, logFileName);
        if (!File.Exists(logFilePath))
            File.WriteAllText(logFilePath, "Registro de ángulos capturados\n");

        // Configura puerto
        stream = new SerialPort(portName, baudRate)
        {
            ReadTimeout = readTimeout,
            DtrEnable = true
        };
        stream.DataReceived += OnSerialData;
        stream.Open();
        Debug.Log("Conexión serial abierta.");

        // Calibrar automáticamente 1 segundo después de iniciar
        Invoke(nameof(Calibrate), 1.0f);
    }

    void OnDestroy()
    {
        if (stream != null)
        {
            stream.DataReceived -= OnSerialData;
            if (stream.IsOpen) stream.Close();
        }
    }

    private void OnSerialData(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            string line = stream.ReadLine();
            Debug.Log("Serial Received: " + line);
            lineQueue.Enqueue(line);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Serial read timeout or error: " + ex.Message);
        }
    }

    void Update()
    {
        // Procesa todas las líneas pendientes por frame
        while (lineQueue.TryDequeue(out strReceived))
        {
            Debug.Log("Processing Serial Line: " + strReceived);
            ParseData(strReceived);
            if (isCalibrated)
                ApplyRotations();
        }

        // Captura manual con tecla C
        if (Input.GetKeyDown(KeyCode.C))
            SaveCurrentAngles();
    }

    private void ParseData(string csv)
    {
        strData = csv.Split(',');
        if (strData.Length < 24)
        {
            Debug.LogWarning("Datos incompletos: " + csv);
            return;
        }

        float.TryParse(strData[1].Trim(), out qx1);
        float.TryParse(strData[5].Trim(), out qx2);
        float.TryParse(strData[9].Trim(), out qx3);
        float.TryParse(strData[13].Trim(), out qx4);
        float.TryParse(strData[17].Trim(), out qx5);
        float.TryParse(strData[20].Trim(), out qw6);
        float.TryParse(strData[21].Trim(), out qx6);
        float.TryParse(strData[22].Trim(), out qy6);
        float.TryParse(strData[23].Trim(), out qz6);

        Debug.Log($"Parsed qx1: {qx1}, qx2: {qx2}, qx3: {qx3}, qx4: {qx4}, qx5: {qx5}, qx6: {qx6}, qy6: {qy6}, qz6: {qz6}, qw6: {qw6}");
    }

    private void ApplyRotations()
    {
        // Dedos
        SetFingerRotations(qx1 - initialQx1, b_r_index1, b_r_index2, b_r_index3);
        SetFingerRotations(qx2 - initialQx2, b_r_middle1, b_r_middle2, b_r_middle3);
        SetFingerRotations(qx3 - initialQx3, b_r_ring1,   b_r_ring2,   b_r_ring3);
        SetFingerRotations(qx4 - initialQx4, b_r_pinky1,  b_r_pinky2,  b_r_pinky3);
        SetFingerRotations(qx5 - initialQx5, b_r_thumb1,  b_r_thumb2,  b_r_thumb3);

        // Muñeca
        var adjWrist = new Quaternion(
            qx6 - initialWristRotation.x,
            qy6 - initialWristRotation.y,
            qz6 - initialWristRotation.z,
            qw6 - initialWristRotation.w);
        adjWrist.Normalize();
        b_r_wrist.localRotation = adjWrist;

#if UNITY_EDITOR
        Vector3 inspectorRot = UnityEditor.TransformUtils.GetInspectorRotation(b_r_wrist);
        Debug.Log($"Muñeca (Inspector): {inspectorRot.z:F1}°");
#endif
    }

    private void SetFingerRotations(float delta, Transform ph1, Transform ph2, Transform ph3)
    {
        float baseFactor = 2.4f;
        float rot1 = delta * Mathf.Rad2Deg * baseFactor;
        float rot2 = delta * Mathf.Rad2Deg * baseFactor * 1.5f;
        float rot3 = delta * Mathf.Rad2Deg * baseFactor * 1.25f;

        ph1.localRotation = Quaternion.Euler(0, 0, rot1);
        ph2.localRotation = Quaternion.Euler(0, 0, rot2);
        ph3.localRotation = Quaternion.Euler(0, 0, rot3);

        Debug.Log($"{ph1.name}: {rot1:F1}°, {ph2.name}: {rot2:F1}°, {ph3.name}: {rot3:F1}°");
    }

    private void SaveCurrentAngles()
    {
        using (var sw = File.AppendText(logFilePath))
        {
            sw.WriteLine($"Índice: {(qx1 - initialQx1) * Mathf.Rad2Deg * 2.4f:F1}°");
            sw.WriteLine($"Medio: {(qx2 - initialQx2) * Mathf.Rad2Deg * 2.4f:F1}°");
            sw.WriteLine($"Anular: {(qx3 - initialQx3) * Mathf.Rad2Deg * 2.4f:F1}°");
            sw.WriteLine($"Meñique: {(qx4 - initialQx4) * Mathf.Rad2Deg * 2.4f:F1}°");
            sw.WriteLine($"Pulgar: {(qx5 - initialQx5) * Mathf.Rad2Deg * 2.4f:F1}°");
#if UNITY_EDITOR
            Vector3 inspectorRot = UnityEditor.TransformUtils.GetInspectorRotation(b_r_wrist);
            sw.WriteLine($"Muñeca: {inspectorRot.z:F1}°");
#else
            sw.WriteLine($"Muñeca: {b_r_wrist.localEulerAngles.z:F1}°");
#endif
            sw.WriteLine();
        }
        Debug.Log("Datos capturados y guardados.");
    }

    public void Calibrate()
    {
        initialQx1 = qx1;
        initialQx2 = qx2;
        initialQx3 = qx3;
        initialQx4 = qx4;
        initialQx5 = qx5;
        initialWristRotation = new Quaternion(qx6, qy6, qz6, qw6);
        isCalibrated = true;
        Debug.Log("Calibración completada con valores:");
        Debug.Log($"Qx1: {initialQx1}, Qx2: {initialQx2}, Qx3: {initialQx3}, Qx4: {initialQx4}, Qx5: {initialQx5}");
        Debug.Log($"Wrist: ({initialWristRotation.x}, {initialWristRotation.y}, {initialWristRotation.z}, {initialWristRotation.w})");
    }
}
