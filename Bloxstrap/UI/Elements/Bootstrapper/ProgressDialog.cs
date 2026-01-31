using System.Drawing;
using System.Windows.Forms;

using Bloxstrap.UI.Elements.Bootstrapper.Base;

namespace Bloxstrap.UI.Elements.Bootstrapper
{
    // basically just the modern dialog

    public partial class ProgressDialog : WinFormsDialogBase
    {
        protected override string _message
        {
            get => labelMessage.Text;
            set => labelMessage.Text = value;
        }

        protected override ProgressBarStyle _progressStyle
        {
            get => ProgressBar.Style;
            set => ProgressBar.Style = value;
        }

        protected override int _progressMaximum
        {
            get => ProgressBar.Maximum;
            set => ProgressBar.Maximum = value;
        }

        protected override int _progressValue
        {
            get => ProgressBar.Value;
            set => ProgressBar.Value = value;
        }

        protected override bool _cancelEnabled
        {
            get => buttonCancel.Enabled;
            set => buttonCancel.Enabled = buttonCancel.Visible = value;
        }

        public ProgressDialog()
        {
            InitializeComponent();

            if (App.Settings.Prop.Theme.GetFinal() == Theme.Dark)
            {
                labelMessage.ForeColor = SystemColors.Window;
                buttonCancel.ForeColor = Color.FromArgb(196, 197, 196);
                buttonCancel.Image = Properties.Resources.DarkCancelButton;
                panel1.BackColor = Color.FromArgb(35, 37, 39);
                BackColor = Color.FromArgb(25, 27, 29);
            }

            labelMessage.Text = Strings.Bootstrapper_StylePreview_TextCancel;
            buttonCancel.Text = Strings.Common_Cancel;
            IconBox.BackgroundImage = App.Settings.Prop.BootstrapperIcon.GetIcon().GetSized(128, 128).ToBitmap();

            SetupDialog();

            ProgressBar.RightToLeft = RightToLeft;
            ProgressBar.RightToLeftLayout = RightToLeftLayout;
        }

        private void ButtonCancel_MouseEnter(object sender, EventArgs e)
        {
            if (App.Settings.Prop.Theme.GetFinal() == Theme.Dark)
            {
                buttonCancel.Image = Properties.Resources.DarkCancelButtonHover;
            }
            else
            {
                buttonCancel.Image = Properties.Resources.CancelButtonHover;
            }
        }

        private void ButtonCancel_MouseLeave(object sender, EventArgs e)
        {
            if (App.Settings.Prop.Theme.GetFinal() == Theme.Dark)
            {
                buttonCancel.Image = Properties.Resources.DarkCancelButton;
            }
            else
            {
                buttonCancel.Image = Properties.Resources.CancelButton;
            }
        }

        private void ProgressDialog_Load(object sender, EventArgs e)
        {
            Activate();
        }
    }
}
