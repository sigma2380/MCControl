namespace MCControl
{
   using System;
   using System.Collections.Generic;
   using System.Data;
   using System.Linq;
   using System.Windows.Forms;
   using Renci.SshNet;

   public partial class Form1 : Form
   {
      private ConnectionInfo connInfo;

      private SshClient ssh;

      private string[] worldList;

      private string[] jarList;

      private string defaultDir = "/opt";

      private char[] newLineSep = new char[] { '\n' };

      private char[] spaceSep = new char[] { ' ' };

      private TimerTracker<WorldStatus> cwsTracker = new TimerTracker<WorldStatus> { Timeout = new TimeSpan(0, 0, 1), Value = WorldStatus.NoSSHConnection };

      private BindingSource worldListBindingSource = new BindingSource();

      public Form1()
      {
         InitializeComponent();
         UpdateStatus();
         listBox2.DataSource = worldListBindingSource;
      }

      private enum WorldStatus
      {
         Online,
         Offline,
         Multiple,
         NoSSHConnection,
      }

      private WorldStatus CurrentWorldStatus
      {
         get
         {
            if (!cwsTracker.IsStale)
            {
               return cwsTracker.Value;
            }

            System.Diagnostics.Debug.WriteLine("Calling CWS");
            if (!SshConnected)
            {
               cwsTracker.Value = WorldStatus.NoSSHConnection;
            }
            else
            {
               string res = RunCommand(" ps -ef | grep SCREEN | grep -v grep");
               string[] instances = res.Split(newLineSep, StringSplitOptions.RemoveEmptyEntries);
               if (instances.Length == 1)
               {
                  cwsTracker.Value = WorldStatus.Online;
               }
               else if (instances.Length == 0)
               {
                  cwsTracker.Value = WorldStatus.Offline;
               }
               else
               {
                  cwsTracker.Value = WorldStatus.Multiple;
               }
            }

            return cwsTracker.Value;
         }
      }

      private bool SshConnected
      {
         get
         {
            if (ssh is null)
            {
               return false;
            }

            return ssh.IsConnected;
         }
      }

      private string CurrentWorldDir
      {
         get
         {
            if (CurrentWorldStatus != WorldStatus.Online)
            {
               return "";
            }

            string res = RunCommand(" ps -ef | grep SCREEN | grep -v grep");
            string[] words = res.Split(' ');
            res = words[words.Length - 1];
            res = res.Replace("\n", "");
            res = res.Replace("/server.jar", "");
            return res;
         }
      }

      private string CurrentWorldName
      {
         get
         {
            return CurrentWorldDir.Replace(defaultDir, "").Replace("/", "");
         }
      }

      public void UpdateWorldList()
      {
         if (ssh is null || !ssh.IsConnected)
         {
            listBox1.Items.Clear();
            return;
         }

         worldList = RunCommand($"ls {defaultDir} | grep -v template | grep -v serverjars").Split('\n');
         worldListBindingSource.DataSource = worldList;   
      }

      public void UpdateJarList()
      {
         if (ssh is null || !ssh.IsConnected)
         {
            return;
         }

         jarList = RunCommand($"ls {defaultDir}/serverjars").Split('\n');
         listBox1.DataSource = jarList;
      }

      public void UpdateRunningStatus()
      {
         switch (CurrentWorldStatus)
         {
            case WorldStatus.NoSSHConnection:
               toolStripStatusLabel3.Text = "SSH Not Connected";
               label1.Text = "SSH Not Connected";
               break;
            case WorldStatus.Offline:
               toolStripStatusLabel3.Text = "Server Not Running";
               label1.Text = "Server Not Running";
               break;
            case WorldStatus.Multiple:
               toolStripStatusLabel3.Text = "Multiple Instances";
               label1.Text = "Error: Multiple Instances Running";
               break;
            case WorldStatus.Online:
               toolStripStatusLabel3.Text = "World: " + CurrentWorldName;
               label1.Text = "Running World: " + CurrentWorldName;
               break;
         }
      }

      public string RunCommand(string command)
      {
         if (!ssh.IsConnected)
         {
            return "";
         }

         SshCommand cmd = ssh.CreateCommand(command);
         cmd.Execute();
         return cmd.Result;
      }

      public void UpdateSSHStatus()
      {
         if (ssh is null)
         {
            toolStripStatusLabel1.Text = "";
            toolStripStatusLabel2.Text = "";
         }
         else
         {
            toolStripStatusLabel1.Text = connInfo.Host;
            toolStripStatusLabel2.Text = ssh.IsConnected ? "Connected" : "";
         }
      }

      public void UpdateStatus()
      {
         DateTime dt = DateTime.Now;
         UpdateSSHStatus();
         UpdateWorldList();
         UpdateJarList();
         UpdateRunningStatus();
         UpdateServerLog();
         TimeSpan t = DateTime.Now - dt;
         System.Diagnostics.Debug.WriteLine(t.ToString());
      }

      public void UpdateServerLog()
      {
         if (CurrentWorldStatus != WorldStatus.Online)
         {
            textBox6.Text = "";
         }
         else
         {
            textBox6.Text = RunCommand($"tail -20 {CurrentWorldDir.Replace("/server.jar", "")}/logs/latest.log").Replace("\n", "\r\n");
         }
      }

      private void Connect()
      {
         connInfo = new ConnectionInfo(textBox3.Text, textBox1.Text, new PasswordAuthenticationMethod(textBox1.Text, textBox2.Text));
         ssh = new SshClient(connInfo);
         ssh.Connect();
         UpdateStatus();
      }

      private bool ValidLinuxName(string name)
      {
         if (name.Length == 0) 
         {
            return false;
         }
         string allowableLetters = "abcdefghijklmnopqrstuvwxyz1234567890-_.ABCDEFGHIJKLMNOPQRSTUVWXYZ";

         foreach (char c in name)
         {
            if (!allowableLetters.Contains(c.ToString()))
            {
               return false;
            }
         }

         return true;
      }

      private void SSHConnectButtonClick(object sender, EventArgs e)
      {
         Connect();
      }

      private void PasswordTextboxKeypress(object sender, KeyPressEventArgs e)
      {
         if (e.KeyChar == 13)
         {
            button1.Focus();
            e.Handled = true;
            Connect();
         }
      }

      private void StopWorldButtonClick(object sender, EventArgs e)
      {
         if (CurrentWorldStatus == WorldStatus.Online)
         {
            RunCommand("screen -S mc -p 0 -X stuff ^C");
         }
         else
         {
            MessageBox.Show("World cannot be stopped.  Server is not in a normal running state.");
         }
      }

      private void CreateWorldButtonClick(object sender, EventArgs e)
      {
         string newWorldName = textBox4.Text;
         string serverjar = listBox1.SelectedItem.ToString();
         if (!ValidLinuxName(newWorldName)) {
            MessageBox.Show($"Invalid world name: '{newWorldName}'.  Name must be all letters or numbers.", "Invalid World Name", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
         }

         RunCommand($"mkdir {defaultDir}/{newWorldName}");
         RunCommand($"cp {defaultDir}/template/* {defaultDir}/{newWorldName}");
         RunCommand($"cp {defaultDir}/serverjars/{listBox1.SelectedItem.ToString()} {defaultDir}/{newWorldName}");
         RunCommand($"mv {defaultDir}/{newWorldName}/{listBox1.SelectedItem.ToString()} {defaultDir}/{newWorldName}/server.jar");
         MessageBox.Show($"World {newWorldName} created using {listBox1.SelectedItem.ToString()}", "World Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
         UpdateWorldList();
      }

      private void RunWorldButtonClick(object sender, EventArgs e)
      {
         RunCommand($"cd {defaultDir}/{listBox2.SelectedItem.ToString()} && screen -dmS mc java -Xms1024M -Xmx3G -jar {defaultDir}/{listBox2.SelectedItem.ToString()}/server.jar");
         MessageBox.Show($"World {listBox2.SelectedItem.ToString()} starting.  Game will be playable in about 1 minute.", "World Started", MessageBoxButtons.OK, MessageBoxIcon.Information);
         UpdateStatus();
      }

      private void DeleteWorldButtonClick(object sender, EventArgs e)
      {
         if (MessageBox.Show($"Are you sure you want to delete world {listBox2.SelectedItem.ToString()}?", "Delete World", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
         {
            RunCommand($"rm -rf {defaultDir}/{listBox2.SelectedItem.ToString()}");
         }

         UpdateStatus();
      }

      private void UpdateTimerTick(object sender, EventArgs e)
      {
         UpdateStatus();
      }

      private void CopyWorlButtonClick(object sender, EventArgs e)
      {
         string newWorldName = textBox5.Text;
         if (!ValidLinuxName(newWorldName))
         {
            MessageBox.Show($"Invalid world name: '{newWorldName}'.  Name must be all letters or numbers.", "Invalid World Name", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
         }

         System.Diagnostics.Debug.WriteLine($"cp -r {defaultDir}/{listBox2.SelectedItem.ToString()} {defaultDir}/{newWorldName}");
         RunCommand($"cp -r {defaultDir}/{listBox2.SelectedItem.ToString()} {defaultDir}/{newWorldName}");
         UpdateWorldList();
      }

      private void TabSwitch(object sender, TabControlEventArgs e)
      {
         switch (tabControl1.SelectedTab.Text)
         {
            case "Connection":
               panel4.BackgroundImage = pictureBox1.Image;
               break;
            case "Server Status":
               panel4.BackgroundImage = pictureBox3.Image;
               break;
            case "Worlds":
               panel4.BackgroundImage = pictureBox2.Image;
               break;
            case "Actions":
               panel4.BackgroundImage = pictureBox4.Image;
               break;
         }
      }
   }
}
