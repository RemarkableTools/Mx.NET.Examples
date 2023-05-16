using Mx.NET.SDK.Configuration;
using Mx.NET.SDK.Core.Domain;
using Mx.NET.SDK.Domain.Data.Account;
using Mx.NET.SDK.Domain.Data.Network;
using Mx.NET.SDK.NativeAuthClient;
using Mx.NET.SDK.NativeAuthClient.Entities;
using Mx.NET.SDK.Provider;
using Mx.NET.SDK.TransactionsManager;
using Mx.NET.SDK.WalletConnect;
using Mx.NET.SDK.WalletConnect.Models.Events;
using QRCoder;
using System.Diagnostics;
using WalletConnectSharp.Core.Models.Pairing;
using WalletConnectSharp.Events.Model;
using Address = Mx.NET.SDK.Core.Domain.Values.Address;

namespace WinForms
{
    public partial class MainForm : Form
    {
        const string CHAIN_ID = "D";
        const string PROJECT_ID = "c7d3aa2b21836c991357e8a56c252962";

        IWalletConnect WalletConnect { get; set; }

        private readonly NativeAuthClient _nativeAuthToken = default!;

        readonly MultiversxProvider Provider = new(new MultiversxNetworkConfiguration(Network.DevNet));
        NetworkConfig NetworkConfig { get; set; } = default!;
        Account Account { get; set; } = default!;

        public MainForm()
        {
            InitializeComponent();
            this.ActiveControl = qrCodeImg;
            CheckForIllegalCrossThreadCalls = false;

            var metadata = new Metadata()
            {
                Name = "Mx.NET.WinForms",
                Description = "Mx.NET.WinForms login testing",
                Icons = new[] { "https://devnet.remarkable.tools/remarkabletools.ico" },
                Url = "https://devnet.remarkable.tools/"
            };
            WalletConnect = new WalletConnect(metadata, PROJECT_ID, CHAIN_ID);
            _nativeAuthToken = new(new NativeAuthClientConfig()
            {
                Origin = metadata.Name,
                ExpirySeconds = 14400,
                BlockHashShard = 2
            });
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            LogMessage("Looking for wallet connection...", SystemColors.ControlText);

            var hasConnection = await WalletConnect.GetConnection();
            WalletConnect.OnSessionUpdateEvent += OnSessionUpdateEvent;
            WalletConnect.OnSessionEvent += OnSessionEvent;
            WalletConnect.OnSessionDeleteEvent += OnSessionDeleteEvent;
            WalletConnect.OnSessionExpireEvent += OnSessionDeleteEvent;
            WalletConnect.OnTopicUpdateEvent += OnTopicUpdateEvent;

            if (hasConnection)
            {
                NetworkConfig = await NetworkConfig.GetFromNetwork(Provider);
                Account = Account.From(await Provider.GetAccount(WalletConnect.Address));

                qrCodeImg.Visible = false;
                btnConnect.Visible = false;
                btnDisconnect.Visible = true;

                LogMessage("Wallet connected", Color.ForestGreen);
            }
            else
            {
                LogMessage("Connect with xPortal App", SystemColors.ControlText);
            }
            btnConnect.Enabled = true;
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            //BtnConnect_Click(sender, e);
        }

        private void LogMessage(string message, Color? color = null)
        {
            tbConnectionStatus.ForeColor = color ?? Color.Black;
            tbConnectionStatus.Text = message;
        }

        private void OnSessionUpdateEvent(object? sender, GenericEvent<SessionUpdateEvent> @event)
        {
            LogMessage("Wallet connected", Color.ForestGreen);
        }

        private void OnSessionEvent(object? sender, GenericEvent<SessionEvent> @event)
        {
            Debug.WriteLine("Session Event");
        }

        private void OnSessionDeleteEvent(object? sender, EventArgs e)
        {
            btnConnect.Visible = true;
            btnDisconnect.Visible = false;

            LogMessage("Wallet disconnected", Color.Firebrick);
        }

        private void OnTopicUpdateEvent(object? sender, GenericEvent<TopicUpdateEvent> @event)
        {
            Debug.WriteLine("Topic Update Event");
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            await WalletConnect.Initialize();

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(WalletConnect.URI, QRCodeGenerator.ECCLevel.Q);
            QRCode qrCode = new QRCode(qrCodeData);
            qrCodeImg.BackgroundImage = qrCode.GetGraphic(4);
            qrCodeImg.Visible = true;

            LogMessage("Waiting for wallet connection...", Color.Blue);

            try
            {
                var authToken = await _nativeAuthToken.GenerateToken();
                await WalletConnect.Connect(authToken);
                qrCodeImg.Visible = false;
                btnConnect.Visible = false;
                btnDisconnect.Visible = true;

                Debug.WriteLine(WalletConnect.Signature);

                NetworkConfig = await NetworkConfig.GetFromNetwork(Provider);
                Account = Account.From(await Provider.GetAccount(WalletConnect.Address));

                LogMessage("Wallet connected", Color.ForestGreen);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                LogMessage("Wallet connection was not approved", Color.Gold);
            }
        }

        private async void BtnDisconnect_Click(object sender, EventArgs e)
        {
            await WalletConnect.Disconnect();
            btnConnect.Visible = true;
            btnDisconnect.Visible = false;

            LogMessage("Wallet disconnected", Color.Firebrick);
        }

        private async void BtnSend_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbReceiver.Text) || string.IsNullOrWhiteSpace(tbEGLD.Text)) return;

            await Account.Sync(Provider);

            var transaction =
                EGLDTransactionRequest.EGLDTransfer(
                NetworkConfig,
                Account,
                Address.FromBech32(tbReceiver.Text),
                ESDTAmount.EGLD(tbEGLD.Text),
                $"TX");

            try
            {
                var transactionRequestDto = await WalletConnect.Sign(transaction);
                var response = await Provider.SendTransaction(transactionRequestDto);
                MessageBox.Show($"Transaction sent to network");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception: {ex.Message}");
            }
        }

        private async void BtnSendMultiple_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbReceiver.Text) || string.IsNullOrWhiteSpace(tbEGLD.Text)) return;

            await Account.Sync(Provider);

            var transaction1 = EGLDTransactionRequest.EGLDTransfer(
                NetworkConfig,
                Account,
                Address.FromBech32(tbReceiver.Text),
                ESDTAmount.EGLD(tbEGLD.Text));
            Account.IncrementNonce();

            var transaction2 = EGLDTransactionRequest.EGLDTransfer(
                NetworkConfig,
                Account,
                Address.FromBech32(tbReceiver.Text),
                ESDTAmount.EGLD(tbEGLD.Text),
                "TX");
            Account.IncrementNonce();

            var transaction3 = EGLDTransactionRequest.EGLDTransfer(
                NetworkConfig,
                Account,
                Address.FromBech32(tbReceiver.Text),
                ESDTAmount.EGLD(tbEGLD.Text),
                "tx AB");
            Account.IncrementNonce();

            var transactions = new[] { transaction1, transaction2, transaction3 };
            try
            {
                var transactionsRequestDto = await WalletConnect.MultiSign(transactions);
                var response = await Provider.SendTransactions(transactionsRequestDto);
                MessageBox.Show($"Transactions sent to network");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception: {ex.Message}");
            }
        }
    }
}