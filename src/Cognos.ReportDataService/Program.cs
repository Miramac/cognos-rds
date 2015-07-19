using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using NLog;

namespace Cognos.ReportDataService
{
    class Program
    {
        static void Main(string[] args)
        {

            var report = new Report(XElement.Load("report.xml"));
            Console.ReadKey();
        }
    }




    public enum ItemType
    {
        ListTable,
        CrossTable,
        Unknown

    }


    static class Helper
    {


        public static string Unescape(string input)
        {
            return Regex.Unescape(input.Replace("__", " ").Replace("_x00", "\\u00"));
        }

        /// <summary>
        /// Get item type
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static ItemType GetReportItemType(XElement xml)
        {
            //List
            if (TryGetElementValue(xml, "lst") != null)
            {
                return ItemType.ListTable;
            }
            //Crosstab
            if (TryGetElementValue(xml, "ctab") != null)
            {
                return ItemType.CrossTable;
            }

            return ItemType.Unknown;
        }

        public static string SetStyleRef(XElement xml)
        {
            return String.Format("{0};{1}", TryGetElementValue(xml, "style", ""), TryGetElementValue(xml, "ref", ""));
        }

        public static string TryGetElementValue(this XElement parentEl, string elementName, string defaultValue = null)
        {
            var foundEl = parentEl.Element(elementName);

            if (foundEl != null)
            {
                return foundEl.Value;
            }

            return defaultValue;
        }
    }


    /// <summary>
    /// Cognos Report
    /// </summary>
    class Report
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        public List<report> Pages { get; private set; }
        public Report(XElement xml)
        {
            Pages = new List<report>();
            //Get pages
            IEnumerable<XElement> pages = xml.Descendants("page");
            logger.Debug("ReportPages: {0}", pages.Count());
            var reportPages = new List<report>();
            foreach (XElement page in pages)
            {
                var Page = new report(page);
                Pages.Add(Page);
            }

        }
    }

    /// <summary>
    /// a report page
    /// </summary>
    class report
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public string Name { get; private set; }
        public XElement Xml { get; private set; }
        public List<Item> Items { get; private set; }

        public report(XElement xml)
        {
            this.Xml = xml;
            this.Name = xml.Element("id").Value;
            this.Items = new List<Item>();
            logger.Debug("Page: {0}", this.Name);

            foreach (XElement obj in xml.Elements("body").Elements())
            {
                var reportItem = new Item(obj);
                Items.Add(reportItem);
            }

        }

    }

    class Item
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public string Name { get; private set; }
        public XElement Xml { get; private set; }
        public ItemType Type { get; private set; }
        public object[,] Data { get; private set; }
        public ListTable ListTable { get; private set; }
        public CrossTable CrossTable { get; private set; }

        public Item(XElement xml)
        {
            this.Xml = xml;
            this.Type = Helper.GetReportItemType(xml);
            logger.Debug("  Tpye: {0}", Type);
            if (this.Type == ItemType.ListTable)
            {
                this.ListTable = new ListTable(xml.Element("lst"));
                logger.Debug("TableName: {0} | Col count {1} | row count: {2}", this.ListTable.Name, this.ListTable.TitelRow.Cells.Count(), this.ListTable.Rows.Count());
                logger.Debug("First Col: {0} | Value: {1} ", this.ListTable.TitelRow.Cells.First().Value, this.ListTable.Rows.First().Cells.First().Value);
            }
            if (this.Type == ItemType.CrossTable)
            {
                var ct = new CrossTable(xml.Element("ctab"));
              
            }
        }
    }

    class CrossTable
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public string styleRef { get; private set; }
        public string Name { get; private set; }
        public Cell Corner { get; private set; }
        public List<Cell> ColumnNames { get; private set; }
        public List<Cell> RowNames { get; private set; }
        public List<Row> Rows { get; private set; }


        public CrossTable(XElement xml)
        {
            this.styleRef = Helper.SetStyleRef(xml);
            this.Name = Helper.TryGetElementValue(xml, "id");
            //get corner cell
            var corner = xml.Element("corner").Element("item").Element("txt");
            corner.Add(xml.Element("corner").Element("ctx"));
            this.Corner = new Cell(corner);

            //Merged columns werden nicht beachtet!
            this.ColumnNames = new List<Cell>();
            foreach( var col in xml.Elements("column"))
            {
                this.ColumnNames.Add(new Cell(col.Element("name").Element("item").Element("txt")));
            }

            //Merged rows werden nicht beachtet!
            this.RowNames = new List<Cell>();
            foreach (var col in xml.Elements("row"))
            {
                this.RowNames.Add(new Cell(col.Element("name").Element("item").Element("txt")));
            }

            logger.Debug(this.RowNames.First().Value);



        }
        public class Table
        {
            
        }
    }

    /// <summary>
    /// Cognos List
    /// </summary>
    class ListTable
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public string styleRef { get; private set; }
        public string Name { get; private set; }
        //the Title columns
        public Row TitelRow { get; private set; }
        //the list rows
        public List<Row> Rows { get; private set; }
        //von item erben

        public ListTable(XElement xml)
        {
            this.styleRef = Helper.SetStyleRef(xml);
            this.Name = Helper.TryGetElementValue(xml, "id");
            this.Rows = new List<Row>();

            //title
            var titel = new XElement("row");
            titel.Add(xml.DescendantsAndSelf("colTitle"));
            this.TitelRow = new Row(titel);

            //rows
            var groupRows = xml.Elements("group").Descendants("row");
            logger.Debug(groupRows.Count());
            foreach (XElement row in groupRows)
            {
                Rows.Add(new Row(row));
            }

        }
    }


    class Row
    {
        public string styleRef { get; private set; }
        //Row cells
        public List<Cell> Cells { get; private set; }

        public Row(XElement xml)
        {
            this.styleRef = Helper.SetStyleRef(xml);
            this.Cells = new List<Cell>();
            foreach (XElement cell in xml.Descendants("item"))
            {
                foreach (XElement aw in cell.Elements())
                {

                    Cells.Add(new Cell(aw));
                }
            }
        }
    }

    /// <summary>
    /// item elemnt
    /// </summary>
    class Cell
    {
        public string styleRef { get; private set; }
        public string ctx { get; private set; }
        public string Value { get; private set; }
        public string valTyp { get; private set; }
        public string fmtVal { get; private set; }
        public string fmtPatrn { get; private set; }
        public string exclPatrn { get; private set; }

        public Cell(XElement xml)
        {

            this.Value = Helper.TryGetElementValue(xml, "val");
            this.styleRef = Helper.SetStyleRef(xml);

            this.ctx = Helper.TryGetElementValue(xml, "ctx");
            this.valTyp = Helper.TryGetElementValue(xml, "valTyp");
            this.fmtVal = Helper.TryGetElementValue(xml, "fmtVal");
            this.fmtPatrn = Helper.TryGetElementValue(xml, "fmtPatrn");
            this.exclPatrn = Helper.TryGetElementValue(xml, "exclPatrn");
        }

    }
}
