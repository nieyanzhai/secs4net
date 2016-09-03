﻿using System;
using System.ComponentModel;
using System.Windows.Forms;
using Secs4Net;
using System.Net;
using System.Drawing;
using System.Threading.Tasks;

namespace SecsDevice {
    public partial class Form1 : Form {
        SecsGem _secsGem;
        readonly ISecsGemLogger Logger;
        readonly BindingList<ReceivedMessage> recvBuffer = new BindingList<ReceivedMessage>();

        public Form1() {
            InitializeComponent();

            radioActiveMode.DataBindings.Add("Enabled", btnEnable, "Enabled");
            radioPassiveMode.DataBindings.Add("Enabled", btnEnable, "Enabled");
            txtAddress.DataBindings.Add("Enabled", btnEnable, "Enabled");
            numPort.DataBindings.Add("Enabled", btnEnable, "Enabled");
            numDeviceId.DataBindings.Add("Enabled", btnEnable, "Enabled");
            recvMessageBindingSource.DataSource = recvBuffer;
            Application.ThreadException += (sender, e) => MessageBox.Show(e.Exception.ToString());

            Logger = new SecsLogger(this);
        }

        private async void btnEnable_Click(object sender, EventArgs e)
        {
            _secsGem?.Dispose();
            _secsGem = new SecsGem(
                radioActiveMode.Checked,
                IPAddress.Parse(txtAddress.Text),
                (int)numPort.Value,
                Logger,
                primaryMsgHandler: (primaryMsg, replyAction) =>
                    this.Invoke(new MethodInvoker(() =>
                        recvBuffer.Add(new ReceivedMessage
                        {
                            Msg = primaryMsg,
                            ReplyAction = replyAction
                        }))));

            _secsGem.ConnectionChanged += delegate
            {
                this.Invoke((MethodInvoker)delegate
                {
                    lbStatus.Text = _secsGem.State.ToString();
                });
            };

            btnEnable.Enabled = false;
            await _secsGem.StartAsync();
            btnDisable.Enabled = true;
        }

        private void btnDisable_Click(object sender, EventArgs e)
        {
            _secsGem?.Dispose();
            _secsGem = null;
            btnEnable.Enabled = true;
            btnDisable.Enabled = false;
            lbStatus.Text = "Disable";
            recvBuffer.Clear();
        }

        private async void btnSendPrimary_Click(object sender, EventArgs e)
        {
            if (_secsGem.State != ConnectionState.Selected)
                return;
            if (string.IsNullOrWhiteSpace(txtSendPrimary.Text))
                return;

            try
            {
                var reply = await _secsGem.SendAsync(txtSendPrimary.Text.ToSecsMessage());
                txtRecvSecondary.Text = reply.ToSML();
            }
            catch (SecsException ex)
            {
                txtRecvSecondary.Text = ex.Message;
            }
        }

        private void lstUnreplyMsg_SelectedIndexChanged(object sender, EventArgs e) {
            var receivedMessage = lstUnreplyMsg.SelectedItem as ReceivedMessage;
            txtRecvPrimary.Text = receivedMessage?.Msg.ToSML();
        }

        private void btnReplySecondary_Click(object sender, EventArgs e)
        {
            var recv = lstUnreplyMsg.SelectedItem as ReceivedMessage;
            if (recv == null)
                return;

            if (string.IsNullOrWhiteSpace(txtReplySeconary.Text))
                return;

            recv.ReplyAction(txtReplySeconary.Text.ToSecsMessage());
            recvBuffer.Remove(recv);
            txtRecvPrimary.Clear();
        }

        class SecsLogger : ISecsGemLogger
        {
            readonly Form1 _form;
            internal SecsLogger(Form1 form)
            {
                _form = form;
            }
            public void TraceMessageIn(SecsMessage msg, int systembyte)
            {
                _form.Invoke((MethodInvoker)delegate {
                    _form.richTextBox1.SelectionColor = Color.Black;
                    _form.richTextBox1.AppendText($"<-- [0x{systembyte:X8}] {msg.ToSML()}\n");
                });
            }

            public void TraceMessageOut(SecsMessage msg, int systembyte)
            {
                _form.Invoke((MethodInvoker)delegate {
                    _form.richTextBox1.SelectionColor = Color.Black;
                    _form.richTextBox1.AppendText($"--> [0x{systembyte:X8}] {msg.ToSML()}\n");
                });
            }

            public void TraceInfo(string msg)
            {
                _form.Invoke((MethodInvoker)delegate {
                    _form.richTextBox1.SelectionColor = Color.Blue;
                    _form.richTextBox1.AppendText($"{msg}\n");
                });
            }

            public void TraceWarning(string msg)
            {
                _form.Invoke((MethodInvoker)delegate {
                    _form.richTextBox1.SelectionColor = Color.Green;
                    _form.richTextBox1.AppendText($"{msg}\n");
                });
            }

            public void TraceError(string msg, Exception ex = null)
            {
                _form.Invoke((MethodInvoker)delegate {
                    _form.richTextBox1.SelectionColor = Color.Red;
                    _form.richTextBox1.AppendText($"{msg}\n");
                    _form.richTextBox1.SelectionColor = Color.Gray;
                    _form.richTextBox1.AppendText($"{ex?.ToString()}\n");
                });
            }

            public void TraceDebug(string msg)
            {
                _form.Invoke((MethodInvoker)delegate {
                    _form.richTextBox1.SelectionColor = Color.Yellow;
                    _form.richTextBox1.AppendText($"{msg}\n");
                });
            }
        }
    }

    public sealed class ReceivedMessage {
        public SecsMessage Msg { get; set; }
        public Action<SecsMessage> ReplyAction { get; set; }
    }
}