using ESRI.ArcGIS.Carto;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.esriSystem; // 用于 IDataStatistics
using System.Collections;

namespace WindowsFormsMap1
{
    public partial class DataoSourceForm : Form
    {
        public IMap CurrentMap { get; set; }
        private IFeatureLayer currentFeatureLayer;
        public DataoSourceForm()
        {
            InitializeComponent();
        }

        private void DataoSourseForm_Load(object sender, EventArgs e)
        {
            if (CurrentMap != null)
            {
                comboBoxLayerName.Items.Clear();
                for (int i = 0; i < CurrentMap.LayerCount; i++)
                {
                    ILayer layer = CurrentMap.get_Layer(i);
                    // 必须是矢量图层才能进行属性查询
                    if (layer is IFeatureLayer)
                    {
                        comboBoxLayerName.Items.Add(layer.Name);
                    }
                }
                if (comboBoxLayerName.Items.Count > 0)
                    comboBoxLayerName.SelectedIndex = 0; // 触发 SelectedIndexChanged 事件
            }
        }

        private void comboBoxLayerName_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxLayerName.SelectedItem == null || CurrentMap == null) return;

            string layerName = comboBoxLayerName.SelectedItem.ToString();

            // 1. 遍历找到对应的图层对象
            for (int i = 0; i < CurrentMap.LayerCount; i++)
            {
                ILayer layer = CurrentMap.get_Layer(i);

                // 找到名字匹配且是矢量图层的
                if (layer.Name == layerName && layer is IFeatureLayer)
                {
                    // 【修正点】这里不能赋值给 CurrentMap，要赋值给 currentFeatureLayer
                    currentFeatureLayer = layer as IFeatureLayer;
                    break;
                }
            }

            // 2. 清空界面上的列表和输入框
            listBoxFields.Items.Clear();
            // 请确保你界面上的右侧列表控件改名为 listBoxValues，如果没有改，这里写 listBox1.Items.Clear();
            listBoxValues.Items.Clear();
            txtWhere.Clear();

            // 3. 读取字段到左侧 ListBox
            // 【修正点】使用 currentFeatureLayer 而不是 _currentLayer
            if (currentFeatureLayer != null)
            {
                // 更新 Label 提示 (如果你界面上有 label3 或其他显示 "Select * From..." 的控件)
                lblSelectInfo.Text = "SELECT * FROM " + currentFeatureLayer.Name + " WHERE:";

                IFields fields = currentFeatureLayer.FeatureClass.Fields;
                for (int i = 0; i < fields.FieldCount; i++)
                {
                    IField field = fields.get_Field(i);
                    string name = field.Name;

                    // 排除 shape 字段 (可选)
                    if (field.Type != esriFieldType.esriFieldTypeGeometry)
                    {
                        // 添加字段名，并加上双引号
                        listBoxFields.Items.Add("\"" + name + "\"");
                    }
                }
            }
        }

        private void listBoxFields_DoubleClick(object sender, EventArgs e)
        {
            if (listBoxFields.SelectedItem != null)
            {
                // 在当前光标位置插入字段名，并加一个空格
                string textToInsert = listBoxFields.SelectedItem.ToString() + " ";

                // 使用 SelectedText 可以替换选中的文本，或者在光标处插入
                int index = txtWhere.SelectionStart;
                txtWhere.Text = txtWhere.Text.Insert(index, textToInsert);

                // 调整光标位置到插入文本的后面
                txtWhere.SelectionStart = index + textToInsert.Length;
                txtWhere.Focus();
            }
        }

        private void listBoxValues_DoubleClick(object sender, EventArgs e)
        {
            if (listBoxValues.SelectedItem != null)
            {
                // 逻辑同上，在光标处插入
                string textToInsert = listBoxValues.SelectedItem.ToString() + " ";
                int index = txtWhere.SelectionStart;
                txtWhere.Text = txtWhere.Text.Insert(index, textToInsert);
                txtWhere.SelectionStart = index + textToInsert.Length;
                txtWhere.Focus();
            }
        }

        private void btnGetUnique_Click(object sender, EventArgs e)
        {
            if (currentFeatureLayer == null || listBoxFields.SelectedItem == null) return;

            try
            {
                this.Cursor = Cursors.WaitCursor;

                // 1. 获取字段名（去掉引号）
                string sFieldName = listBoxFields.SelectedItem.ToString().Replace("\"", "");

                // 2. 【关键修正】先获取这个字段的详细信息，用来判断它是什么类型
                int fieldIndex = currentFeatureLayer.FeatureClass.Fields.FindField(sFieldName);
                if (fieldIndex == -1) return;
                IField pField = currentFeatureLayer.FeatureClass.Fields.get_Field(fieldIndex);

                // 判断是否为字符串类型字段
                bool isStringField = (pField.Type == esriFieldType.esriFieldTypeString);

                // 3. 统计唯一值
                ICursor pCursor = currentFeatureLayer.Search(null, false) as ICursor;
                IDataStatistics pDataStatistics = new DataStatisticsClass();
                pDataStatistics.Cursor = pCursor;
                pDataStatistics.Field = sFieldName;

                IEnumerator pEnumerator = pDataStatistics.UniqueValues;
                pEnumerator.Reset();

                listBoxValues.Items.Clear();

                while (pEnumerator.MoveNext())
                {
                    object value = pEnumerator.Current;
                    if (value != null)
                    {
                        // 4. 【关键修正】根据字段类型决定要不要加单引号
                        if (isStringField)
                        {
                            // 如果字段本身是文本型（比如 Name），加单引号
                            listBoxValues.Items.Add("'" + value.ToString() + "'");
                        }
                        else
                        {
                            // 如果字段是数值型（比如 FID, Area, Population），直接显示，不加引号
                            listBoxValues.Items.Add(value.ToString());
                        }
                    }
                }
                System.Runtime.InteropServices.Marshal.ReleaseComObject(pCursor);
            }
            catch (Exception ex)
            {
                MessageBox.Show("获取失败: " + ex.Message);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }
        private void AddTextToQuery(string text)
        {
            // 前后加空格，避免粘连，比如 "Name"='A' 变成 "Name" = 'A'
            string insertText = " " + text + " ";
            int index = txtWhere.SelectionStart;
            txtWhere.Text = txtWhere.Text.Insert(index, insertText);
            txtWhere.SelectionStart = index + insertText.Length;
            txtWhere.Focus();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            AddTextToQuery("=");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            AddTextToQuery("<>");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            AddTextToQuery("Like");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            AddTextToQuery(">");
        }

        private void button5_Click(object sender, EventArgs e)
        {
            AddTextToQuery(">=");
        }

        private void button6_Click(object sender, EventArgs e)
        {
            AddTextToQuery("AND");
        }

        private void button7_Click(object sender, EventArgs e)
        {
            AddTextToQuery("<");
        }

        private void button8_Click(object sender, EventArgs e)
        {
            AddTextToQuery("<=");
        }

        private void button9_Click(object sender, EventArgs e)
        {
            AddTextToQuery("OR");
        }
        private void ExecuteQuery(bool closeForm)
        {
            if (currentFeatureLayer == null)
            {
                MessageBox.Show("未选择图层！");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtWhere.Text))
            {
                MessageBox.Show("请输入查询条件！");
                return;
            }

            try
            {
                // 1. 定义查询过滤器
                IQueryFilter queryFilter = new QueryFilterClass();
                queryFilter.WhereClause = txtWhere.Text;

                // 2. 执行选择
                IFeatureSelection featureSelection = currentFeatureLayer as IFeatureSelection;

                // 默认使用“新建选择集”，如果你做了 cmbMethod 下拉框，可以用下面的 switch
                esriSelectionResultEnum selectionType = esriSelectionResultEnum.esriSelectionResultNew;

                // 如果你有 comboBoxMethod，请取消下面的注释
                /*
                if (comboBoxMethod.SelectedIndex == 1) selectionType = esriSelectionResultEnum.esriSelectionResultAdd;
                else if (comboBoxMethod.SelectedIndex == 2) selectionType = esriSelectionResultEnum.esriSelectionResultXOR;
                else if (comboBoxMethod.SelectedIndex == 3) selectionType = esriSelectionResultEnum.esriSelectionResultAnd;
                */

                featureSelection.SelectFeatures(queryFilter, selectionType, false);

                // 3. 刷新主地图视图 (高亮显示)
                IActiveView activeView = CurrentMap as IActiveView;
                // 只刷新选择集部分，效率更高
                activeView.PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);

                // 4. (可选) 提示查到了多少个
                // int count = featureSelection.SelectionSet.Count;
                // MessageBox.Show($"查询成功，共选中 {count} 个要素。");

                if (closeForm)
                {
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("查询失败，请检查SQL语句格式。\n\n错误详情:\n" + ex.Message);
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            ExecuteQuery(true);
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            ExecuteQuery(false);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            // 1. 清空查询条件文本框
            txtWhere.Clear();

            // 2. (可选) 将光标重新聚焦到文本框，方便直接重新输入
            txtWhere.Focus();
        }
    }
}
