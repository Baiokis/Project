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
    private float checkInterval = 1f; // Verificar a conexão a cada 5 segundos
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



    // Start é chamado antes da primeira atualização do quadro
    void Start()
    {
        this.timer_wd = DateTime.Now;

        this.ConnectAsync();
        this.TCP_Post(controles.id);
    }

    // A atualização é chamada uma vez por quadro
    void Update()
    {
        // Obter comandos vindo dos controles
        this.UpdateControllers();

        this.TriggerFunctionManager();

        if (client == null || (!connecting && !client.Connected))
        {
            this.ConnectAsync();
        }
    }


    private void UpdateControllers()
    {
        controles = new Controllers(); // Criar uma nova instância de Controles

        TimeSpan timeElapsed = DateTime.Now - timer_wd;
        float milliseconds = (float)timeElapsed.TotalMilliseconds;

        if (milliseconds > 300)
        {
            this.WD = !this.WD;
            controles.wd = this.WD;
        }

        UpdateLeftController();
        UpdateRightController();
    }

    private void UpdateLeftController()
    {
        if (OVRInput.IsControllerConnected(OVRInput.Controller.LTouch))
        {
            controles.g_ie = GetAxis1DValue(OVRInput.RawAxis1D.LIndexTrigger);
            controles.g_me = GetAxis1DValue(OVRInput.RawAxis1D.LHandTrigger);
            controles.b_y = OVRInput.Get(OVRInput.Button.Four);
            controles.b_x = OVRInput.Get(OVRInput.Button.Three);
            controles.b_e = OVRInput.Get(OVRInput.Button.PrimaryThumbstick);
            controles.a_ex = GetAxis2DValue(OVRInput.Axis2D.PrimaryThumbstick, 'x');
            controles.a_ey = GetAxis2DValue(OVRInput.Axis2D.PrimaryThumbstick, 'y');
            controles.p_ex = GetLocalControllerPositionValue(OVRInput.Controller.LTouch, 'x');
            controles.p_ey = GetLocalControllerPositionValue(OVRInput.Controller.LTouch, 'y');
            controles.p_ez = GetLocalControllerPositionValue(OVRInput.Controller.LTouch, 'z');
            controles.r_ex = GetLocalControllerRotationValue(OVRInput.Controller.LTouch, 'x');
            controles.r_ey = GetLocalControllerRotationValue(OVRInput.Controller.LTouch, 'y');
            controles.r_ez = GetLocalControllerRotationValue(OVRInput.Controller.LTouch, 'z');
            controles.r_ew = GetLocalControllerRotationValue(OVRInput.Controller.LTouch, 'w');
        }
    }

    private void UpdateRightController()
    {
        if (OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
        {
            controles.g_id = GetAxis1DValue(OVRInput.RawAxis1D.RIndexTrigger);
            controles.g_md = GetAxis1DValue(OVRInput.RawAxis1D.RHandTrigger);
            controles.b_a = OVRInput.Get(OVRInput.Button.One);
            controles.b_b = OVRInput.Get(OVRInput.Button.Two);
            controles.b_d = OVRInput.Get(OVRInput.Button.SecondaryThumbstick);
            controles.a_dx = GetAxis2DValue(OVRInput.Axis2D.SecondaryThumbstick, 'x');
            controles.a_dy = GetAxis2DValue(OVRInput.Axis2D.SecondaryThumbstick, 'y');
            controles.p_dx = GetLocalControllerPositionValue(OVRInput.Controller.RTouch, 'x');
            controles.p_dy = GetLocalControllerPositionValue(OVRInput.Controller.RTouch, 'y');
            controles.p_dz = GetLocalControllerPositionValue(OVRInput.Controller.RTouch, 'z');
            controles.r_dx = GetLocalControllerRotationValue(OVRInput.Controller.RTouch, 'x');
            controles.r_dy = GetLocalControllerRotationValue(OVRInput.Controller.RTouch, 'y');
            controles.r_dz = GetLocalControllerRotationValue(OVRInput.Controller.RTouch, 'z');
            controles.r_dw = GetLocalControllerRotationValue(OVRInput.Controller.RTouch, 'w');
        }
    }

    private int GetAxis1DValue(OVRInput.RawAxis1D axis)
    {
        return (int)(OVRInput.Get(axis) * 1000);
    }

    private int GetAxis2DValue(OVRInput.Axis2D axis2D, char component)
    {
        Vector2 value = OVRInput.Get(axis2D);
        float componentValue = (component == 'x') ? value.x : value.y;
        return (int)(componentValue * 1000);
    }

    private int GetLocalControllerPositionValue(OVRInput.Controller controller, char component)
    {
        Vector3 position = OVRInput.GetLocalControllerPosition(controller);
        float componentValue = (component == 'x') ? position.x : (component == 'y') ? position.y : position.z;
        return (int)(componentValue * 1000);
    }

    private int GetLocalControllerRotationValue(OVRInput.Controller controller, char component)
    {
        Quaternion rotation = OVRInput.GetLocalControllerRotation(controller);
        float componentValue = (component == 'x') ? rotation.x : (component == 'y') ? rotation.y : (component == 'z') ? rotation.z : rotation.w;
        return (int)(componentValue * 1000);
    }

    public async void VibrateController(OVRInput.Controller controller, float intensity, float duration, float frequency)
    {
        StartCoroutine(DoVibration(controller, intensity, duration, frequency));
    }

    private System.Collections.IEnumerator DoVibration(OVRInput.Controller controller, float intensity, float duration, float frequency)
    {
        float startTime = Time.time;
        float endTime = startTime + duration;
        float interval = frequency == 0 ? 0 : 1f / frequency;

        while (Time.time < endTime)
        {
            OVRInput.SetControllerVibration(intensity, intensity, controller);
            if (interval > 0)
            {
                yield return new WaitForSeconds(interval);
                OVRInput.SetControllerVibration(0, 0, controller);
                yield return new WaitForSeconds(interval);
            }
        }

        OVRInput.SetControllerVibration(0, 0, controller);
    }


    #region Comunicação TCP

    private async void ConnectAsync()
    {
        UnityEngine.Debug.LogWarning("Connection lost. Reconnecting...");

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

            UnityEngine.Debug.Log("Connected to server.");

            this.ReceiveAsync();
        }
        catch (SocketException ex)
        {
            //UnityEngine.Debug.Log($"Error connecting to server: {ex.Message}");

            await Task.Delay(ConnectionAttemptDelayMs); 
            connecting = false;

            this.ConnectAsync(); // Tentar novamente
        }
        catch (Exception) { }
    }


    private async Task ReceiveAsync()
    {
        if (reading || !connected)
            return;

        reading = true;
        byte[] buffer = null; // Declaração da variável buffer fora do bloco try

        try
        {
            // Inicializar o TcpClient e outras configurações

            receivedData = new List<byte>();
            buffer = ArrayPool<byte>.Shared.Rent(1024); // Atribuir o valor do pool de arrays

            StringBuilder stringBuilder = new StringBuilder();

            while (client.Connected)
            {
                int bytesRead = await client.GetStream().ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    // Adicionar os dados recebidos à lista
                    for (int i = 0; i < bytesRead; i++)
                    {
                        receivedData.Add(buffer[i]);
                    }

                    // Processar os dados conforme necessário

                    // Limpar o StringBuilder, já que não é mais necessário
                    stringBuilder.Clear();
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.Log($"Erro ao receber mensagem via TCP-IP: {ex.Message}");
        }
        finally
        {
            reading = false;
            if (buffer != null) // Verificar se o buffer não é nulo antes de retorná-lo
                ArrayPool<byte>.Shared.Return(buffer);
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }

    /// <summary>
    /// Comunicacao TCP-IP: POST
    /// </summary>
    /// <param name="message">Dados a serem enviados pela rede TCP-IP</param>
    public async void TCP_Post(string message)
    {
        if (client == null || !connected)
            return;

        // Comprime o JSON usando Gzip
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
            // Codificação e envio de dados comprimidos
            await client.GetStream().WriteAsync(bytes_comprimidos, 0, bytes_comprimidos.Length).ConfigureAwait(false);

            //UnityEngine.Debug.Log($"Sent (compressed): {message}");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.Log($"Erro ao enviar mensagem via TCP-IP: {ex.Message}");
        }
    }
    #endregion


    #region Funções periódicas
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
    #endregion

    private void OnApplicationQuit()
    {
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
