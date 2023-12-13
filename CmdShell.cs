using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Drawing;

namespace CmdShell
{
    public partial class CmdShell : Form
    {
        string IP = "239.148.35.70";
        int Port = 49155;
        bool alive = false;
        UdpClient client;
        bool isNetActive = false;
        string lastSendedMessage;
        public CmdShell()
        {
            InitializeComponent();
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            ExtractFolderDialog.SelectedPath = Environment.CurrentDirectory;
            if (File.Exists("CmdShellConfig"))
            {
                try
                {
                    string[] settings = File.ReadAllLines("CmdShellConfig");
                    IP = settings[0];
                    Port = Convert.ToInt32(settings[1]);
                    ReplyTextBox.Text += "\r\nНастройки из файла \"CmdShellConfig\" загружены.\r\n";
                }
                catch
                { MessageBox.Show("Ошибка чтения профиля.", "Ошибка | CmdShell", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
            ReplyTextBox.Text += "Программа успешно запущена и готова к работе.\r\n\r\n";
        }

        public void RunWithRedirect(string command)
        {
            if (Autoshielding.Checked == true)
            {
                command = command.Replace("/", @"\/");
            }
            try
            {
                if (isNetActive == true)
                {
                    if (CommandTextBox.Text.Length == 0)
                    { }
                    else
                    {
                        try
                        {
                            var message = "C/NDatagramN/" + PCName.Text + "/NDatagramN/";
                            if (CMD.Checked)
                            { message += "CMD/NDatagramN/"; }
                            if (WPS.Checked)
                            { message += "PWS/NDatagramN/"; }
                            if (Cletter.Checked)
                                message += "/c ";
                            message += CommandTextBox.Text;
                            lastSendedMessage = message;
                            byte[] data = Encoding.Unicode.GetBytes(message);
                            client.Send(data, data.Length, IP, Port);
                            ReplyTextBox.Invoke(new Action(() => ReplyTextBox.Text += "Отправка команды: " + command + "\r\n"));
                        }
                        catch
                        {
                            MessageBox.Show("Ошибка отправки датаграммы.", "Ошибка | CmdShell", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                else
                {
                    try
                    {
                        using (Process p = new Process())
                        {
                            var proc = new Process();

                            if (CMD.Checked)
                                proc.StartInfo.FileName = "cmd";
                            else
                                proc.StartInfo.FileName = "powershell";
                            if (Cletter.Checked)
                                proc.StartInfo.Arguments = "/c " + command;
                            else
                                proc.StartInfo.Arguments = command;
                            proc.StartInfo.RedirectStandardOutput = true;
                            proc.StartInfo.RedirectStandardError = true;
                            proc.EnableRaisingEvents = true;
                            proc.StartInfo.CreateNoWindow = true;
                            proc.ErrorDataReceived += proc_DataReceived;
                            proc.OutputDataReceived += proc_DataReceived;
                            proc.StartInfo.UseShellExecute = false;
                            proc.Start();
                            proc.BeginErrorReadLine();
                            proc.BeginOutputReadLine();
                            proc.WaitForExit();
                        }
                    }
                    catch
                    { MessageBox.Show("Ошибка выполнения команды.", "Ошибка | CmdShell", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            }
            catch
            {
            }
        }

        public void proc_DataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(e.Data);
                byte[] newBytes = Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding(1251), bytes);
                string text = Encoding.GetEncoding(866).GetString(newBytes);
                ReplyTextBox.Invoke(new Action(() => ReplyTextBox.Text = ReplyTextBox.Text + text + "\r\n"));
            }
            catch { }
        }
        private void CleanForm_Click(object sender, EventArgs e)
        {
            ReplyTextBox.Clear();
            CommandTextBox.Text = "help";
        }
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            ReplyTextBox.SelectionStart = ReplyTextBox.Text.Length;
            ReplyTextBox.ScrollToCaret();
        }
        private async void Send_Click(object sender, EventArgs e)
        {
            ReplyTextBox.Text = ReplyTextBox.Text + "\r\n\r\n";

            await Task.Run(() => RunWithRedirect(CommandTextBox.Text));
        }
        private void Cletter_CheckedChanged(object sender, EventArgs e)
        {
            if (Cletter.Checked == false)
                MessageBox.Show("Вы собираетесь отключить добавление ключа c/. Команда, отправленная без этого ключа, может привести к зависанию программы.", "Отключение ключа \"C/\" | CmdShell", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                Send_Click(sender, e);
        }

        private void NetModeBTN_Click(object sender, EventArgs e)
        {
            myTimer.Interval = 3000;
            myTimer.Enabled = true;
            client = new UdpClient(Port);
            client.JoinMulticastGroup(IPAddress.Parse(IP), 20);
            Task receiveTask = new Task(ReceiveMessages);
            receiveTask.Start();
            ReplyTextBox.Text += "\r\nСетевой режим активирован. Вводимые команды будут транслироваться, а не выполняться локально.\r\nНачато прослушивание канала: " + IP + ":" + Port + "\r\n";
            isNetActive = true;
            DisableNetModeBTN.Enabled = true;
            groupBox2.Enabled = true;
            NetModeBTN.Enabled = false;
        }
        private void ReceiveMessages()
        {
            alive = true;
            try
            {
                while (alive)
                {
                    bool techmessage = false;
                    IPEndPoint remoteIp = null;
                    byte[] data = client.Receive(ref remoteIp);
                    string message = Encoding.Unicode.GetString(data);
                    try
                    {
                        if (message == (DateTime.Now.ToString("dd")))
                        {
                            techmessage = true;
                        }
                    }
                    catch
                    { }
                    {
                        Invoke(new MethodInvoker(() =>
                        {
                            if (techmessage == false)
                            {
                                var splittedmessage = message.Split(new string[] { "/NDatagramN/" }, StringSplitOptions.None);
                                if (splittedmessage[0] == "A")
                                {
                                    string time = DateTime.Now.ToShortTimeString();
                                    ReplyTextBox.Text = ReplyTextBox.Text + time + " ответ от " + splittedmessage[1] + ": " + splittedmessage[3] + "\r\n";
                                }
                                else
                                { ReplyTextBox.Text += "Датаграмма: " + message + "\r\n"; }
                            }
                            else
                            {
                            }
                        }));
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                if (!alive)
                    return;
                throw;
            }
            catch
            {
            }
        }
        private void NETping_Click(object sender, EventArgs e)
        {
            ReplyTextBox.Text += "\r\n";
            byte[] data = Encoding.Unicode.GetBytes("CmdShellNetModePing");
            client.Send(data, data.Length, IP, Port);
        }
        private void SetEveryone_Click(object sender, EventArgs e)
        {
            PCName.Text = "CmdShellDestinationEveryone";
        }
        private void DisableNetModeBTN_Click(object sender, EventArgs e)
        {
            alive = false;
            client.Close();
            NetModeBTN.Enabled = true;
            DisableNetModeBTN.Enabled = false;
            groupBox2.Enabled = false;
            isNetActive = false;
            ReplyTextBox.Text += "\r\nСетевой режим отключён. Вводимые команды будут выполняться локально.\r\n";
            myTimer.Enabled = false;
        }
        private void CreateFile_Click(object sender, EventArgs e)
        {
            try
            {
                StreamWriter writefl;
                writefl = File.CreateText("CmdShellConfig");
                writefl.WriteLine(IP);
                writefl.Write(Port);
                writefl.Close();
                MessageBox.Show("Профиль успешно сохранён в файл \"CmdShellConfig\".", "Профиль сохранён | CmdShell", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                MessageBox.Show("Произошла ошибка сохранения файла. Убедитесь, что директория существует, и программе предоставлены все разрешения для чтения и записи.", "Ошибка | CmdShell", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void myTimer_Tick_1(object sender, EventArgs e)
        {
            try
            {
                string message = (DateTime.Now.ToString("dd"));
                byte[] data = Encoding.Unicode.GetBytes(message);
                client.Send(data, data.Length, IP, Port);
            }
            catch
            {
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("CmdShell - оболочка для стандартных утилит Windows CMD и POWERSHELL. Поддерживается возможность передачи команд и получения ответов по локальной сети через широковещательную рассылку. Имеется возможность сохранения файла с сетевыми настройками.\r\n\r\nv.1.0: первая тестовая версия.\r\n\r\nv.2.0: исправлено зависание интерфейса во время выполнения команды.\r\n\r\nv.3.0: добавлена возможность посылать команды по локальной сети, создано приложение-приёмник команд. Добавлена возможность создать файл с сетевыми настройками.\r\n\r\nv.3.1: Мелкие исправления и улучшения.\r\n\r\n\r\n\r\n073797@gmail.com\r\n(c) by Naulex, 2023.", "О программе | CmdShell", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExtractNetCmdShell_Click(object sender, EventArgs e)
        {
            if (ExtractFolderDialog.ShowDialog() == DialogResult.Cancel)
                return;
            File.WriteAllBytes(ExtractFolderDialog.SelectedPath + "/NetCmdShell.exe", CmdWpfShell.Properties.Resources.NetCmdShell);
            MessageBox.Show("Удалённый приёмник команд извлечён. Скопируйте его на необходимый ПК и запустите.", "Извлечение завершено | CmdShell", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

