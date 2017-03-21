using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.IO;
using System.Drawing.Imaging;

namespace imageDBtoServer
{
    public partial class Form1 : Form
    {
        SqlConnection conn = new SqlConnection("Data Source = mypc; Initial Catalog = imageDB; User ID = sa; Password = Cash2me!!");
        SqlCommand command;
        string imgLoc = "";
        public Form1()
        {
            InitializeComponent();
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog dlg = new OpenFileDialog();
                dlg.Filter = "JPG Files (*.jpg) |*.jpg| PNG Files (*.png)|*.png| All Files (*.*)|*.*";
                dlg.Title = "Select User ID Image";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    imgLoc = dlg.FileName.ToString();
                    picID.ImageLocation = imgLoc;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            try
            {
                byte[] img = null;
                FileStream fs = new FileStream(imgLoc, FileMode.Open, FileAccess.Read);
                BinaryReader br = new BinaryReader(fs);
                img = br.ReadBytes((int)fs.Length);
                string sql = "INSERT INTO Images(userID, firstName, lastName, ImageID)Values("+textBoxUserID.Text+",'"+textBoxFirstName.Text+"','"+textBoxLastName.Text+"', @img)";
                if (conn.State != ConnectionState.Open)
                    conn.Open();
                command = new SqlCommand(sql, conn);
                command.Parameters.Add(new SqlParameter("@img", img));
                int x = command.ExecuteNonQuery();
                conn.Close();
                MessageBox.Show(x.ToString() + " record(s) saved.");
                textBoxUserID.Text = "";
                textBoxFirstName.Text = "";
                textBoxLastName.Text = "";
                picID.Image = null;
            }
            catch (Exception ex)
            {
                conn.Close();
                MessageBox.Show(ex.Message);
            }
        }

        private void buttonDisplay_Click(object sender, EventArgs e)
        {
            try
            {
                string sql = "SELECT userID, firstName, lastName, ImageID FROM Images WHERE userID = "+textBoxUserID.Text+"";
                if (conn.State != ConnectionState.Open)
                    conn.Open();
                command = new SqlCommand(sql, conn);
                SqlDataReader reader = command.ExecuteReader();
                reader.Read();
                if(reader.HasRows)
                {
                    textBoxFirstName.Text = reader[1].ToString();
                    textBoxLastName.Text = reader[2].ToString();
                    byte[] img = (byte[])(reader[3]);
                    if (img == null)
                        picID.Image = null;
                    else
                    {
                        MemoryStream ms = new MemoryStream(img);
                        picID.Image = Image.FromStream(ms);
                    }
                }
                else
                {
                    textBoxFirstName.Text = "";
                    textBoxLastName.Text = "";
                    picID.Image = null;
                    MessageBox.Show("This does not Exist.");
                }
                conn.Close();
            }
            catch(Exception ex)
            {
                conn.Close();
                MessageBox.Show(ex.Message);
            }
        }

        private void textBoxUserID_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
