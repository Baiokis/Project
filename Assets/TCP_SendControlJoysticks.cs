using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.IO.Compression;

public class TCP_SendControlJoysticks : MonoBehaviour
{
    private Controllers controles = new Controllers();
    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    // Informações do Servidor para realizar a conexão
    private string IP = "192.168.31.51";
    public int TCP_PORTA;
    private const int ConnectionAttemptDelayMs = 1000;

    private TcpClient client;
    private byte[] receiveBuffer = ArrayPool<byte>.Shared.Rent(1024);
    private StringBuilder stringBuilder = new StringBuilder();
    private float checkInterval = 1f;
    private List<byte> receivedData = new List<byte>();
    private string jsonData = "";
    private float timer = 0f;

    private bool connected = false;
    private bool connecting = false;
    private bool reading = false;
    private bool fireL = false;
    private bool fireR = false;
    private bool WD = false;

    // Temporização [seg]
    private float timerInterval0 = 0.1f;
    private float elapsedTime0 = 0.0f;
    private DateTime timer_wd;

    void Start()
    {
        this.timer_wd = DateTime.Now;
        ConnectAsync();
        TCP_Post(controles.id);
    }

    void Update()
    {
        UpdateControllers();
        TriggerFunctionManager();

        if (client == null || (!connecting && !client.Connected))
        {
            ConnectAsync();
        }
    }

    private void UpdateControllers()
    {
        controles = new Controllers();
        TimeSpan timeElapsed = DateTime.Now - timer_wd;
        float milliseconds = (float)timeElapsed.TotalMilliseconds;

        if (milliseconds > 300)
        {
            this.WD = !this.WD;
            controles.wd = this.WD;
        }

        UpdateLeftController();
        UpdateRightController();
        UnityEngine.Debug.Log($"Controles atualizados: {JsonUtility.ToJson(controles)}");
    }

    private void UpdateLeftController()
    {
        // Implementação específica...
    }

    private void UpdateRightController()
    {
        // Implementação específica...
    }

    // Métodos GetAxis1DValue, GetAxis2DValue, GetLocalControllerPositionValue, GetLocalControllerRotationValue...

    private async void ConnectAsync()
    {
        UnityEngine.Debug.Log("Tentando conectar...");

        try
        {
            connected = false;
            connecting = true;

            if (client != null)
                client.Close();
            client = new TcpClient();

            await client.ConnectAsync(IPAddress.Parse(IP), TCP_PORTA);

            connected = true;
            connecting = false;

            UnityEngine.Debug.Log("Conectado ao servidor.");
            ReceiveAsync();
        }
        catch (SocketException ex)
        {
            UnityEngine.Debug.LogError($"Erro de conexão: {ex.Message}");
            await Task.Delay(ConnectionAttemptDelayMs);
            connecting = false;
            ConnectAsync();
        }
    }

    private async Task ReceiveAsync()
    {
        if (reading || !connected)
            return;

        reading = true;
        byte[] buffer = null;

        try
        {
            receivedData = new List<byte>();
            buffer = ArrayPool<byte>.Shared.Rent(1024);

            while (client.Connected)
            {
                int bytesRead = await client.GetStream().ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    UnityEngine.Debug.Log("Dados recebidos.");
                    for (int i = 0; i < bytesRead; i++)
                    {
                        receivedData.Add(buffer[i]);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Erro ao receber dados: {ex.Message}");
        }
        finally
        {
            reading = false;
            if (buffer != null)
                ArrayPool<byte>.Shared.Return(buffer);
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }

    public async void TCP_Post(string message)
    {
        if (client == null || !connected)
            return;

        byte[] bytes_comprimidos;
        using (var ms = new MemoryStream())
        {
            using (var gzip = new GZipStream(ms, CompressionMode.Compress))
            {
                using (var sw = new StreamWriter(gzip))
                {
                    sw.Write(message);
                }
            }
            bytes_comprimidos = ms.ToArray();
        }

        try
        {
            await client.GetStream().WriteAsync(bytes_comprimidos, 0, bytes_comprimidos.Length).ConfigureAwait(false);
            UnityEngine.Debug.Log("Dados enviados.");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Erro ao enviar dados: {ex.Message}");
        }
    }

    private void TriggerFunctionManager()
    {
        SendDataControll_TCPIP();
    }

    private void SendDataControll_TCPIP()
    {
        string jsonDataAux = JsonUtility.ToJson(controles, false);
        if (jsonData != jsonDataAux)
        {
            jsonData = jsonDataAux;
            TCP_Post(jsonData);
        }
    }

    private void OnApplicationQuit()
    {
        UnityEngine.Debug.Log("Aplicação encerrando, fechando conexão TCP.");
        connected = false;
        if (client != null)
            client.Close();
    }
}

[Serializable]
public class Controllers
{
    // Identificação do controlador
    public string id = "Quest2_Autvix_E20130-FAPES";

    // Gatilhos dos indicadores
    public int g_id;
    public int g_ie;

    // Gatilhos das mãos
    public int g_md;
    public int g_me;

    // Botões
    public bool b_a;
    public bool b_b;
    public bool b_y;
    public bool b_x;
    public bool b_d;
    public bool b_e;
    public bool wd;

    // Analógicos
    public int a_dx;
    public int a_dy;
    public int a_ex;
    public int a_ey;

    // Posição
    public int p_dx;
    public int p_dy;
    public int p_dz;
    public int p_ex;
    public int p_ey;
    public int p_ez;

    // Rotação
    public int r_dx;
    public int r_dy;
    public int r_dz;
    public int r_dw;
    public int r_ex;
    public int r_ey;
    public int r_ez;
    public int r_ew;

    public Controllers()
    {
        // Inicialize todos os valores como zero ou falso
        g_id = 0;
        g_ie = 0;
        g_md = 0;
        g_me = 0;
        b_a = false;
        b_b = false;
        b_y = false;
        b_x = false;
        b_d = false;
        b_e = false;
        wd = false;
        a_dx = 0;
        a_dy = 0;
        a_ex = 0;
        a_ey = 0;
        p_dx = 0;
        p_dy = 0;
        p_dz = 0;
        p_ex = 0;
        p_ey = 0;
        p_ez = 0;
        r_dx = 0;
        r_dy = 0;
        r_dz = 0;
        r_dw = 0;
        r_ex = 0;
        r_ey = 0;
        r_ez = 0;
        r_ew = 0;
    }
}
