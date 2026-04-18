using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Util.Store;
using Newtonsoft.Json.Linq;
using Svc.Shared; // Tham chiếu đến Project DLL Registry
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace SvcSetup
{
    public partial class frmSetup : Form
    {
        public frmSetup()
        {
            InitializeComponent();
        }

        private void btnBrowseJson_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "JSON Files (*.json)|*.json";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtJsonPath.Text = ofd.FileName;
                }
            }
        }

        private void btnBrowseFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtLocalPath.Text = fbd.SelectedPath;
                }
            }
        }

        private async void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtJsonPath.Text) ||
                string.IsNullOrEmpty(txtLocalPath.Text) ||
                string.IsNullOrEmpty(txtDriveUrl.Text))
            {
                MessageBox.Show("Vui lòng điền đầy đủ thông tin!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnSave.Enabled = false;
            lblStatus.Text = "Trạng thái: Đang xác thực với Google...";

            try
            {
                // 1. Đọc Client ID và Secret từ file JSON
                string jsonContent = File.ReadAllText(txtJsonPath.Text);
                JObject googleConfig = JObject.Parse(jsonContent);
                string clientId = googleConfig["installed"]?["client_id"]?.ToString();
                string clientSecret = googleConfig["installed"]?["client_secret"]?.ToString();

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    throw new Exception("File JSON không đúng định dạng Google Desktop App.");
                }

                // 2. Thực hiện OAuth2 Login
                string tokenFolder = @"C:\ProgramData\Svc\Tokens";
                if (!Directory.Exists(tokenFolder)) Directory.CreateDirectory(tokenFolder);

                var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                    new[] { DriveService.Scope.Drive },
                    "user",
                    CancellationToken.None,
                    new FileDataStore(tokenFolder, true));

                // 3. Trích xuất Drive Folder ID
                string driveId = ExtractDriveId(txtDriveUrl.Text);

                // 4. Lưu vào Registry bằng DLL Shared
                RegistryHelper.SaveConfig(txtLocalPath.Text, driveId, clientId, clientSecret);

                lblStatus.Text = "Trạng thái: Hoàn tất! Vui lòng khởi động lại Service.";
                MessageBox.Show("Cấu hình đã được lưu thành công vào Registry.\nService sẽ tự động nhận diện trong vòng 1 phút.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.Close();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Trạng thái: Lỗi cấu hình.";
                MessageBox.Show("Có lỗi xảy ra: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSave.Enabled = true;
            }
        }

        private string ExtractDriveId(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            if (!url.Contains("folders/")) return url;

            try
            {
                var parts = url.Split(new[] { "folders/" }, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    return parts[1].Split('?')[0].Split('/')[0];
                }
            }
            catch { }
            return url;
        }
    }
}