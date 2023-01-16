using Mx.NET.SDK.Configuration;
using Mx.NET.SDK.Core.Domain;
using Mx.NET.SDK.Domain;
using Mx.NET.SDK.Domain.Data.Account;
using Mx.NET.SDK.Domain.Data.Network;
using Mx.NET.SDK.Provider;
using Mx.NET.SDK.TransactionsManager;
using Mx.NET.SDK.WalletConnect;
using QRCoder;
using WalletConnectSharp.Core;
using WalletConnectSharp.Core.Models;
using Address = Mx.NET.SDK.Core.Domain.Values.Address;

namespace WinForms
{
    public partial class MainForm : Form
    {
        IWalletConnect? IWalletConnect { get; set; }
        NetworkConfig? networkConfig { get; set; }
        Account? account;
        MultiversxProvider provider = new(new MultiversxNetworkConfiguration(Network.DevNet));

        public MainForm()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
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

        private void OnSessionConnectEvent(object sender, WalletConnectSession session)
        {
            LogMessage("Wallet connected", Color.ForestGreen);
        }

        private void OnSessionDisconnectEvent(object sender, EventArgs e)
        {
            LogMessage("Wallet disconnected", Color.Firebrick);
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            var clientMeta = new ClientMeta()
            {
                Name = "Mx.NET.WinForms",
                Description = "Mx.NET.WinForms login testing",
                Icons = new[] { "https://remarkable.tools/favicon.ico" },
                URL = "https://remarkable.tools/"
            };

            IWalletConnect = new WalletConnect(clientMeta);
            IWalletConnect.OnSessionConnected += OnSessionConnectEvent;
            IWalletConnect.OnSessionDisconnected += OnSessionDisconnectEvent;

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(IWalletConnect.URI, QRCodeGenerator.ECCLevel.Q);
            QRCode qrCode = new QRCode(qrCodeData);
            pictureBox1.BackgroundImage = qrCode.GetGraphic(4);

            LogMessage("Waiting for wallet connection...", Color.Blue);

            try
            {
                await IWalletConnect.Connect();
                networkConfig = await NetworkConfig.GetFromNetwork(provider);
                account = Account.From(await provider.GetAccount(IWalletConnect.Address));
            }
            catch (TaskCanceledException tex)
            {
                LogMessage("Wallet connection was not approved", Color.Gold);
            }
        }

        private async void BtnDisconnect_Click(object sender, EventArgs e)
        {
            await IWalletConnect.Disconnect();
        }

        private async void BtnSend_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbReceiver.Text) || string.IsNullOrWhiteSpace(tbEGLD.Text)) return;

            await account.Sync(provider);

            var transaction =
                EGLDTransactionRequest.EGLDTransfer(
                networkConfig,
                account,
                Address.FromBech32(tbReceiver.Text),
                ESDTAmount.EGLD(tbEGLD.Text),
                $"SEND {tbEGLD.Text}");

            try
            {
                var transactionRequestDto = await IWalletConnect.Sign(transaction);
                var response = await provider.SendTransaction(transactionRequestDto);
                MessageBox.Show($"Transaction sent to network");
            }
            catch (WalletException wex)
            {
                MessageBox.Show($"Wallet Exception: {wex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception: {ex.Message}");
            }
        }

        private async void BtnSendMultiple_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbReceiver.Text) || string.IsNullOrWhiteSpace(tbEGLD.Text)) return;

            await account.Sync(provider);

            var transaction1 = EGLDTransactionRequest.EGLDTransfer(
                networkConfig,
                account,
                Address.FromBech32(tbReceiver.Text),
                ESDTAmount.EGLD(tbEGLD.Text),
                "Tx 1");
            account.IncrementNonce();

            var transaction2 = EGLDTransactionRequest.EGLDTransfer(
                networkConfig,
                account,
                Address.FromBech32(tbReceiver.Text),
                ESDTAmount.EGLD(tbEGLD.Text),
                "Tx 2");
            account.IncrementNonce();

            var transaction3 = EGLDTransactionRequest.EGLDTransfer(
                networkConfig,
                account,
                Address.FromBech32(tbReceiver.Text),
                ESDTAmount.EGLD(tbEGLD.Text),
                "Tx 3");
            account.IncrementNonce();

            var transactions = new TransactionRequest[] { transaction1, transaction2, transaction3 };
            try
            {
                var transactionsRequestDto = await IWalletConnect.MultiSign(transactions);
                var response = await provider.SendTransactions(transactionsRequestDto);
                MessageBox.Show($"Transactions sent to network");
            }
            catch (WalletException wex)
            {
                MessageBox.Show($"Wallet Exception: {wex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception: {ex.Message}");
            }
        }
    }
}