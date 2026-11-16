using BarcodeLib;
using HiQPdf;
using HISWEBAPI.DTO;
using HISWEBAPI.Models;
using HISWEBAPI.Repositories.Implementations;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services.Interfaces;
using HISWEBAPI.Utilities;
using iTextSharp.text.pdf;
using MessagingToolkit.QRCode.Codec;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using Barcode = BarcodeLib.Barcode;
using Image = System.Drawing.Image;
using PdfDocument = HiQPdf.PdfDocument;
using PdfImage = HiQPdf.PdfImage;
using PdfPage = HiQPdf.PdfPage;
using PdfRectangle = HiQPdf.PdfRectangle;
namespace HISWEBAPI.Services.Implementations
{


    public class PatientInvestigationReportPdfService : IPatientInvestigationReportPdfService
    {
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(PatientInvestigationReportPdfService));

        private const string HiQPdfSerialCode = "g8vq0tPn‐5c/q4fHi‐8fq7rbOj‐sqO3o7uy‐t6Owsq2y‐sa26urq6";
        private PatientInvestigationReportRequest _request = new();
        private AllGlobalValues _globalValues = new();
        private string _baseUrl = string.Empty;

        private readonly ILabReportRepository _printSettingRepository;   // NEW
        private readonly IPatientLabReport _patientLabReport;

        public PatientInvestigationReportPdfService(
            IPatientLabReport patientLabReport,
            ILabReportRepository printSettingRepository)                 // NEW
        {
            _patientLabReport = patientLabReport;
            _printSettingRepository = printSettingRepository;
        }


        PdfLayoutInfo html1LayoutInfo;
        PdfDocument document = new PdfDocument();
        int MarginLeft = 28;
        int PageWidth = 540;
        int BrowserWidth = 840;
        int HeaderHeight = 210;
        int XHeader = 20;
        int YHeader = 17;
        int HeaderBrowserWidth = 805;
        bool TopLine = false;
        bool BottomLine = true;

        int FooterHeight = 110;
        int XFooter = -10;
        int YFooter = 100;
        int FooterBrowserWidth = 800;
        bool footerTopLine = false;

        bool showDigitalSinature = true;
        int XSignature = 10;
        int YSignature = 0;

        int userId = 0;
        int branchId = 0;

        string headerMasterString = string.Empty;
        string footerMasterString = string.Empty;

        string patientHeaderString = string.Empty;
        string patientFooterString = string.Empty;

        string patientInvestigationIds = string.Empty;

        string patientContacts = string.Empty;
        string patientEmailIds = string.Empty;
        string defaultNABLLogoPath = string.Empty;
        string externalPdfPath = string.Empty;
        int isHeaderPNG = 0;
        int isLogoPNG = 0;
        bool isFirstRow = true;
        bool hideInvtNameforSingleTest = false;
        bool isPrintDepartmentSeperate = false;

        bool isSendEmail = false;
        bool isSendWhtsAppMessage = false;
        string remarksTestWise = "";
        string[] remarksArrayTestWise = new string[0];

        //Barcode Setup
        bool Barcode = true;
        int XBarcode = 155;
        int YBarcode = 128;
        int BarCodeHeight = 15;
        int BarCodeWidth = 180;

        bool QRCode = true;
        int XQRcode = 510;
        int YQRcode = 55;
        int QRHeight = 71;
        int QRWidth = 71;

        //------------------------



        int BarcodePosition = 0;
        int RotateBarcode90Degree = 0;

        int QRCodePosition = 0;


        int PrintDateTime = 0;
        int PreparedBy = 0;
        int PrintedBy = 0;
        int PageNumbering = 0;

        int dummyMode = 0;

        //------------------------ Report style (overridable from LabReportPrintSettingMaster)
        // fonts (px, relative to BrowserWidth)
        string FontFamily = "Arial";
        int BaseFontSize = 14;
        int InvestigationNameFontSize = 16;
        int TableHeaderFontSize = 14;
        int MethodFontSize = 13;

        // colours
        string ThemeColor = "#B94A52";
        string ThemeTextColor = "#FFFFFF";
        string TableHeaderBgColor = "#D9D9D9";
        string TableHeaderTextColor = "#000000";
        string AltRowColor = "#F2F2F2";
        string GridLineColor = "#DDDDDD";
        string AbnormalResultColor = "#000000";

        // spacing (px)
        int RowSpacing = 3;
        int InvestigationSpacing = 14;

        // bold
        bool ObservationNameBold = false;
        bool ResultBoldIfAbnormal = true;

        // show / hide
        bool ShowInvestigationBand = true;
        bool ShowSampleType = true;
        bool ShowAccessionInfo = true;
        bool ShowDepartmentName = false;
        bool ShowMethodColumn = true;
        bool ShowStripedRows = true;
        bool ShowGridLines = true;
        bool ShowEndOfReport = true;

        int stripeIndex = 0;

        private static readonly System.Text.RegularExpressions.Regex ColorRegex =
            new(@"^(#[0-9A-Fa-f]{3}|#[0-9A-Fa-f]{6}|[A-Za-z]{3,20})$");
        private static readonly System.Text.RegularExpressions.Regex FontRegex =
            new(@"^[A-Za-z0-9 ,'\-]{1,100}$");

        DataRow drRunning;
        DataRow drPrevious = null;
        DataTable dtPatientInvestigations = new DataTable();

        /// <summary>Loads print settings (branch value -> default row) from DB and applies them.</summary>
        private void ApplyPrintSettings(int branch)
        {
            // preview API (DummyMode 3) always reads fresh values so direct table edits show immediately
            var s = _printSettingRepository.GetEffectiveSetting(branch, refreshCache: dummyMode == PatientLabReport.PrintSettingPreviewMode);
            if (s.Count == 0) return;                       // keep hard-coded fallbacks

            // value missing / null / invalid -> keep the current (hard-coded default) value
            int I(string key, int current) => s.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : current;
            bool B(string key, bool current) => s.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n == 1 : current;
            string C(string key, string current) => s.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) && ColorRegex.IsMatch(v.Trim()) ? v.Trim() : current;
            string F(string key, string current) => s.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) && FontRegex.IsMatch(v.Trim()) ? v.Trim() : current;
            int Size(string key, int current, int min, int max) { int n = I(key, current); return n < min || n > max ? current : n; }

            // style: fonts
            FontFamily = F("FontFamily", FontFamily);
            BaseFontSize = Size("BaseFontSize", BaseFontSize, 6, 40);
            InvestigationNameFontSize = Size("InvestigationNameFontSize", InvestigationNameFontSize, 6, 40);
            TableHeaderFontSize = Size("TableHeaderFontSize", TableHeaderFontSize, 6, 40);
            MethodFontSize = Size("MethodFontSize", MethodFontSize, 6, 40);

            // style: colours
            ThemeColor = C("ThemeColor", ThemeColor);
            ThemeTextColor = C("ThemeTextColor", ThemeTextColor);
            TableHeaderBgColor = C("TableHeaderBgColor", TableHeaderBgColor);
            TableHeaderTextColor = C("TableHeaderTextColor", TableHeaderTextColor);
            AltRowColor = C("AltRowColor", AltRowColor);
            GridLineColor = C("GridLineColor", GridLineColor);
            AbnormalResultColor = C("AbnormalResultColor", AbnormalResultColor);

            // style: spacing / bold / show-hide
            RowSpacing = Size("RowSpacing", RowSpacing, 0, 30);
            InvestigationSpacing = Size("InvestigationSpacing", InvestigationSpacing, 0, 100);
            ObservationNameBold = B("ObservationNameBold", ObservationNameBold);
            ResultBoldIfAbnormal = B("ResultBoldIfAbnormal", ResultBoldIfAbnormal);
            ShowInvestigationBand = B("ShowInvestigationBand", ShowInvestigationBand);
            ShowSampleType = B("ShowSampleType", ShowSampleType);
            ShowAccessionInfo = B("ShowAccessionInfo", ShowAccessionInfo);
            ShowDepartmentName = B("ShowDepartmentName", ShowDepartmentName);
            ShowMethodColumn = B("ShowMethodColumn", ShowMethodColumn);
            ShowStripedRows = B("ShowStripedRows", ShowStripedRows);
            ShowGridLines = B("ShowGridLines", ShowGridLines);
            ShowEndOfReport = B("ShowEndOfReport", ShowEndOfReport);

            // page / body
            MarginLeft = I("MarginLeft", MarginLeft);
            PageWidth = I("PageWidth", PageWidth);
            BrowserWidth = I("BrowserWidth", BrowserWidth);

            // header
            HeaderHeight = I("HeaderHeight", HeaderHeight);
            XHeader = I("XHeader", XHeader);
            YHeader = I("YHeader", YHeader);
            HeaderBrowserWidth = I("HeaderBrowserWidth", HeaderBrowserWidth);
            TopLine = B("TopLine", TopLine);
            BottomLine = B("BottomLine", BottomLine);

            // footer
            FooterHeight = I("FooterHeight", FooterHeight);
            XFooter = I("XFooter", XFooter);
            YFooter = I("YFooter", YFooter);
            FooterBrowserWidth = I("FooterBrowserWidth", FooterBrowserWidth);
            footerTopLine = B("FooterTopLine", footerTopLine);

            // signature
            showDigitalSinature = B("ShowDigitalSignature", showDigitalSinature);
            XSignature = I("XSignature", XSignature);
            YSignature = I("YSignature", YSignature);

            // barcode
            Barcode = B("IsActiveBarcode", Barcode);
            XBarcode = I("XBarcode", XBarcode);
            YBarcode = I("YBarcode", YBarcode);
            BarCodeHeight = I("BarcodeHeight", BarCodeHeight);
            BarCodeWidth = I("BarcodeWidth", BarCodeWidth);
            BarcodePosition = I("BarcodePosition", BarcodePosition);
            RotateBarcode90Degree = I("RotateBarcode90Degree", RotateBarcode90Degree);

            // QR
            QRCode = B("IsActiveQRCode", QRCode);
            XQRcode = I("XQRCode", XQRcode);
            YQRcode = I("YQRCode", YQRcode);
            QRHeight = I("QRHeight", QRHeight);
            QRWidth = I("QRWidth", QRWidth);
            QRCodePosition = I("QRCodePosition", QRCodePosition);

            // footer texts
            PrintDateTime = I("PrintDateTime", PrintDateTime);
            PreparedBy = I("PreparedBy", PreparedBy);
            PrintedBy = I("PrintedBy", PrintedBy);
            PageNumbering = I("PageNumbering", PageNumbering);

            // behaviour
            hideInvtNameforSingleTest = B("HideTestNameForSingleTest", hideInvtNameforSingleTest);
            isPrintDepartmentSeperate = B("PrintDepartmentSeparate", isPrintDepartmentSeperate);
            isSendEmail = B("SendEmail", isSendEmail);
            isSendWhtsAppMessage = B("SendWhatsAppMessage", isSendWhtsAppMessage);
        }
        public PatientInvestigationReportPdfResult GenerateReport(PatientInvestigationReportRequest request, AllGlobalValues globalValues, string baseUrl)
        {
            _request = request;
            _globalValues = globalValues;
            _baseUrl = baseUrl.TrimEnd('/');
            patientInvestigationIds = request.PatientInvestigationIds;
            patientContacts = request.Contacts ?? string.Empty;
            patientEmailIds = request.EmailIds ?? string.Empty;
            branchId = request.BranchId;
            userId =  globalValues.userId;
            isHeaderPNG = request.IsHeaderPng;
            dummyMode = request.DummyMode;
            isSendEmail = false;
            isSendWhtsAppMessage = false;

            html1LayoutInfo = null;
            drRunning = null;
            drPrevious = null;
            dtPatientInvestigations = new DataTable();
            document = new PdfDocument();

            byte[] pdfBuffer = BuildReport(patientInvestigationIds);
            string patientName = GetPatientNameForResponse();

            return new PatientInvestigationReportPdfResult
            {
                Content = pdfBuffer,
                FileName = $"{patientName}_LAB_Report_.pdf",
                HasData = dtPatientInvestigations != null && dtPatientInvestigations.Rows.Count > 0
            };
        }

        /// <summary>
        /// Cleans filename by removing invalid characters
        /// </summary>
        private string CleanFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "PatientReport";

            // Remove invalid file name characters
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(
                new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            fileName = System.Text.RegularExpressions.Regex.Replace(fileName, invalidRegStr, "_");

            // Replace spaces with underscores
            fileName = fileName.Replace(" ", "_");

            // Limit length for safety
            if (fileName.Length > 50)
                fileName = fileName.Substring(0, 50);

            return fileName;
        }

        /// <summary>
        /// Gets patient name for filename from data table
        /// </summary>
        private string GetPatientNameForResponse()
        {
            string patientName = "PatientReport";

            if (dtPatientInvestigations != null && dtPatientInvestigations.Rows.Count > 0)
            {
                DataRow firstRow = dtPatientInvestigations.Rows[0];
                if (firstRow["PName"] != DBNull.Value && !string.IsNullOrEmpty(firstRow["PName"].ToString()))
                {
                    patientName = firstRow["PName"].ToString();
                    patientName = CleanFileName(patientName);

                    if (string.IsNullOrEmpty(patientName))
                        patientName = "PatientReport";
                }
            }

            return patientName;
        }

        private byte[] BuildReport(string patientInvestigationIds)
        {
            document.SerialNumber = HiQPdfSerialCode;

            DataTable dtHeaderFooter = _patientLabReport.GetLabHeaderFooter(branchId, 4, dummyMode);
            headerMasterString = string.Empty;
            footerMasterString = string.Empty;
            foreach (DataRow r in dtHeaderFooter.Rows)
            {
                if (r["IsHeader"].ToString() == "1")
                    headerMasterString = r["HeaderBody"].ToString();
                else
                    footerMasterString = r["HeaderBody"].ToString();
            }

            dtPatientInvestigations = _patientLabReport.GetPatientInvestigationsForReportPrint(branchId, isHeaderPNG, patientInvestigationIds, userId, dummyMode);

            // all print settings (layout + style) are loaded once per report, before the styles are built
            ApplyPrintSettings(branchId);
            isSendEmail = false;
            isSendWhtsAppMessage = false;

            string sbStyles = BuildStyles();

            StringBuilder sb = new StringBuilder();
            List<Tuple<int, byte[], string>> pdfSequence = new List<Tuple<int, byte[], string>>();

            if (dtPatientInvestigations.Rows.Count > 0)
            {




                sb.Append(sbStyles.ToString());
                sb.Append("<table style='width: 98%;  margin:0px ;'>");

                string preDept = string.Empty;
                string preLabNo = string.Empty;
                int preIsPrintAlone = 0;
                int preInvestigationId = 0;
                bool isFirstRow = true;
                bool isPageChanged = false;
                bool headerPrintedOnCurrentPage = false; // NEW: Track if header is printed on current page

                foreach (DataRow dr in dtPatientInvestigations.Rows)
                {
                    //XSignature = int.TryParse(dr["XSign"]?.ToString(), out var FRA) ? FRA : 450;
                    //YSignature = int.TryParse(dr["YSign"]?.ToString(), out var FRB) ? FRB : 10;
                    //HeaderHeight = int.TryParse(dr["HeaderHeight"]?.ToString(), out var FRC) ? FRC : 210;
                    //defaultNABLLogoPath = dr["NABLPath"].ToString();
                    //PrintDateTime = int.TryParse(dr["PrintDateTime"]?.ToString(), out var FRE) ? FRE : 1;
                    //PreparedBy = int.TryParse(dr["PreparedBy"]?.ToString(), out var FRF) ? FRF : 1;
                    //PrintedBy = int.TryParse(dr["PrintedBy"]?.ToString(), out var FRG) ? FRG : 1;
                    //PageNumbering = int.TryParse(dr["PageNumbering"]?.ToString(), out var FRH) ? FRH : 1;
                    //showDigitalSinature = int.TryParse(dr["DoctorSignature"]?.ToString(), out var FRI) && FRI == 1;
                    //hideInvtNameforSingleTest = int.TryParse(dr["TestNameforSingleTest"]?.ToString(), out var FRJ) && FRJ == 1;
                    //isPrintDepartmentSeperate = int.TryParse(dr["PrintDepartmentSeperate"]?.ToString(), out var FRK) && FRK == 1;
                    //isSendEmail = int.TryParse(dr["SendEmail"]?.ToString(), out var FRL) && FRL == 1;
                    //isSendWhtsAppMessage = int.TryParse(dr["SendWhatsAppMessage"]?.ToString(), out var FRM) && FRM == 1;
                    //XQRcode = int.TryParse(dr["XQRCode"]?.ToString(), out var FRN) ? FRN : 510;
                    //YQRcode = int.TryParse(dr["YQRCode"]?.ToString(), out var FRO) ? FRO : 40;
                    //QRHeight = int.TryParse(dr["QRHeight"]?.ToString(), out var FRP) ? FRP : 80;
                    //QRWidth = int.TryParse(dr["QRWidth"]?.ToString(), out var FRQ) ? FRQ : 80;
                    //QRCodePosition = int.TryParse(dr["QRCodePosition"]?.ToString(), out var FRR) ? FRR : 1;
                    //QRCode = int.TryParse(dr["IsActiveQRCode"]?.ToString(), out var FRS) && FRS == 1;
                    //XBarcode = int.TryParse(dr["XBarcode"]?.ToString(), out var FRT) ? FRT : 160;
                    //YBarcode = int.TryParse(dr["YBarcode"]?.ToString(), out var FRU) ? FRU : 120;
                    //BarCodeHeight = int.TryParse(dr["BarcodeHeight"]?.ToString(), out var FRV) ? FRV : 15;
                    //BarCodeWidth = int.TryParse(dr["BarcodeWidth"]?.ToString(), out var FRW) ? FRW : 200;
                    //BarcodePosition = int.TryParse(dr["BarcodePosition"]?.ToString(), out var FRX) ? FRX : 1;
                    //Barcode = int.TryParse(dr["IsActiveBarcode"]?.ToString(), out var FRY) && FRY == 1;
                    //RotateBarcode90Degree = int.TryParse(dr["RotateBarcode90Degree"]?.ToString(), out var FRZ) ? FRZ : 1;


                    string investigationPdfPath = Utility.getString(dr["InvestigationDocumentFile"]);
                    int investigationIdforPDF = Utility.getInt(dr["InvestigationId"]);

                    if (!string.IsNullOrEmpty(investigationPdfPath))
                    {
                        // Split multiple paths (separated by '#')
                        string[] paths = investigationPdfPath.Split('#', (char)StringSplitOptions.RemoveEmptyEntries);

                        foreach (string path in paths)
                        {
                            string convertedPath = path.Replace("-", "\\");

                            if (!convertedPath.Contains(":") && convertedPath.Length > 1)
                            {
                                if (convertedPath.StartsWith("D\\"))
                                    convertedPath = "D:" + convertedPath.Substring(1);
                                else if (convertedPath.StartsWith("C\\"))
                                    convertedPath = "C:" + convertedPath.Substring(1);
                            }

                            // Add each file separately for merging
                            pdfSequence.Add(new Tuple<int, byte[], string>(investigationIdforPDF, null, convertedPath));
                        }
                    }

                    drRunning = dr;
                    if (drPrevious == null)
                        drPrevious = dr;


                    if (preLabNo != dr["LabNo"].ToString()
                            || (preDept != dr["Department"].ToString() && !isFirstRow && isPrintDepartmentSeperate)
                            || (preInvestigationId != Utility.getInt(dr["InvestigationId"]) && (preIsPrintAlone == 1 || Utility.getInt(dr["IsPrintAlone"]) == 1))
                        )
                    {
                        if (sb.Length > 0 && !isFirstRow)
                        {
                            sb.Append("</table>");

                            //// Add footer remarks before page change
                            //string remarks_ = drRunning["ReportFooterRemarks"].ToString();
                            //string[] remarksArray_ = remarks_.Split('#');
                            //if (remarksArray_.Length > 0 && !string.IsNullOrWhiteSpace(remarksArray_[0]))
                            //{
                            //    sb.Append("<div style='font-size: 14px; margin-top: 10px; padding: 4px; background-color: #f8f9fa; border-radius: 4px; font-family: \"Times New Roman\", Times, serif;'>");
                            //    foreach (string remark in remarksArray_)
                            //    {
                            //        if (!string.IsNullOrWhiteSpace(remark))
                            //        {
                            //            string[] parts = remark.Split('@');
                            //            string remarkText = parts[0].Trim();
                            //            int investigationId = parts.Length > 1 && int.TryParse(parts[1], out int id) ? id : -1;

                            //            if (investigationId == 0)
                            //            {
                            //                sb.Append("<div style='margin-bottom: 5px;'>" + remarkText + "</div>");
                            //            }
                            //            if (investigationId == Utility.getInt(drRunning["InvestigationId"]))
                            //            {
                            //                sb.Append("<div style='margin-bottom: 5px;'>" + remarkText + "</div>");
                            //            }
                            //        }
                            //    }
                            //    sb.Append("</div>");
                            //}

                            //// Add end of report message before page change
                            //sb.Append("<div style='text-align: center; font-size: 16px; font-weight: bold; padding: 4px; color: #000000;font-family: \"Times New Roman\", Times, serif; '>************* End of Report *************</div>");

                            html1LayoutInfo = null;
                            AddContent(sb.ToString());
                            sb.Clear();
                            sb.Append(sbStyles.ToString());
                            sb.Append("<table style='width: 98%;  margin: 0 auto;'>");
                            isPageChanged = true;
                            headerPrintedOnCurrentPage = false; // RESET: Header not printed on new page
                        }
                        else
                        {
                            isPageChanged = false;
                        }
                    }

                    // Department name ONLY when lab no / page changes (setting: ShowDepartmentName)
                    if (ShowDepartmentName && (preLabNo != dr["LabNo"].ToString() || isPageChanged) && !headerPrintedOnCurrentPage)
                    {
                        sb.Append("<tr class='deptName'><td colspan='5'> " + dr["Department"].ToString().ToUpper() + " </td></tr>");
                    }

                    if (drRunning["ReportTypeId"].ToString() == "1")//Tabular Report
                    {
                        DataTable dtTabularReport = _patientLabReport.GetPatientTabularReportForPrint(Utility.getInt(dr["PatientInvestigationId"]), dummyMode);
                        bool singleTestMode = hideInvtNameforSingleTest && dtTabularReport.Rows.Count == 1;
                        bool newLabOrPage = preLabNo != dr["LabNo"].ToString() || isPageChanged;
                        bool firstResultRowPrinted = false;

                        // every tabular test gets its own fixed-width table, so columns line up the same way for all tests
                        sb.Append("<tr><td colspan='5' style='padding: 0;'><table class='invTable'>" + InvestigationColGroup());

                        if (ShowInvestigationBand)
                        {
                            // New layout: coloured bar (test name + sample) -> accession line -> column header,
                            // kept together as one block so it never splits across pages
                            if (!singleTestMode)
                            {
                                // the first result row goes inside the title block, so a title can never sit
                                // alone at the bottom of a page with its results on the next page
                                stripeIndex = 0;
                                var firstRow = new StringBuilder();
                                if (dtTabularReport.Rows.Count > 0)
                                    AppendResultRow(firstRow, dtTabularReport.Rows[0], singleTestMode);
                                AppendInvestigationTitle(sb, dr, withColumnHeader: true, firstResultRowHtml: firstRow.ToString());
                                headerPrintedOnCurrentPage = true;
                                firstResultRowPrinted = dtTabularReport.Rows.Count > 0;
                            }
                            else if (!headerPrintedOnCurrentPage)
                            {
                                AppendColumnHeader(sb);
                                headerPrintedOnCurrentPage = true;
                            }
                        }
                        else
                        {
                            // Old layout: one column header per page, then plain test name + method line
                            if (newLabOrPage && !headerPrintedOnCurrentPage)
                            {
                                AppendColumnHeader(sb);
                                headerPrintedOnCurrentPage = true;
                            }

                            if (!singleTestMode)
                            {
                                AppendInvestigationTitle(sb, dr);
                                if (!String.IsNullOrEmpty(dr["InvestigationMethod"].ToString()))
                                    sb.Append("<tr><td colspan='5' class='sampleInfo'><u>Method</u> : " + dr["InvestigationMethod"].ToString() + "</td></tr>");
                            }
                        }

                        if (!firstResultRowPrinted)
                            stripeIndex = 0;
                        for (int r = firstResultRowPrinted ? 1 : 0; r < dtTabularReport.Rows.Count; r++)
                        {
                            AppendResultRow(sb, dtTabularReport.Rows[r], singleTestMode);
                        }



                        //if (!String.IsNullOrEmpty(dr["InvestigationComment"].ToString()))
                        //{
                        //    sb.Append(" <tr>");
                        //    sb.Append("<td colspan='5' style='font-size: 14px; padding: 5px; background-color: #f8f9fa; font-weight:bold'><b></b> " + dr["InvestigationComment"].ToString() + "</td> ");
                        //    sb.Append(" </tr>");
                        //}

                        if (!string.IsNullOrEmpty(dr["InvestigationComment"].ToString()))
                        {
                            AppendRemarks(sb, dr["InvestigationComment"].ToString());
                        }

                        if (!string.IsNullOrEmpty(dr["interpretation"].ToString()))
                        {
                            string interpretationHtml = StyleHtmlTables(
                                System.Net.WebUtility.HtmlDecode(dr["interpretation"].ToString()),
                                "border-collapse:collapse; width:100%; border:1px solid #333;",
                                "border:1px solid #333; padding:4px; text-align:center;",
                                "border:1px solid #333; padding:4px; font-weight:bold; background:#f1f1f1; text-align:center;");

                            sb.Append("<tr>");
                            sb.Append("<td colspan='5' style='font-size:14px; border:1px solid #333; padding:12px; background-color:#f8f9fa;'>");
                            sb.Append("<strong>Interpretation :</strong><br/>");
                            sb.Append(interpretationHtml);
                            sb.Append("</td>");
                            sb.Append("</tr>");
                        }




                        if (!String.IsNullOrEmpty(drRunning["NABLFilePath"].ToString()))
                        {
                            sb.Append(" <tr> ");
                            sb.Append("<td colspan='1' style='text-align: center; padding: 5px; '><img src='" + drRunning["NABLFilePath"].ToString().Replace("-", "\\") + "' style='height: 50px; margin: auto;' /></td> ");
                            sb.Append("<td colspan='4' style='padding: 5px; font-size: 12px; color: #666; '>NABL Logo Description</td> ");
                            sb.Append(" </tr>");
                        }
                        else if (Utility.getInt(drRunning["IsDefaultLogo"]) == 1)
                        {
                            sb.Append(" <tr> ");
                            sb.Append("<td colspan='1' style='text-align: center; padding: 5px; '><img src='" + defaultNABLLogoPath + "' style='height: 50px; margin: auto;' /></td>");
                            sb.Append("<td colspan='4' style='padding: 5px; font-size: 12px; color: #666; '>Tests marked with NABL symbol are accredited by NABL vide Certificate no MC-2139</td> ");
                            sb.Append(" </tr>");
                        }

                        sb.Append("</table></td></tr>");   // end of this test's table
                    }

                    else if (drRunning["ReportTypeId"].ToString() == "2") // Free Text Report
                    {
                        DataTable dtFreeTextReport = _patientLabReport.GetPatientFreeTextReportForPrint(Utility.getInt(dr["PatientInvestigationId"]), dummyMode);

                        AppendInvestigationTitle(sb, dr);

                        // no result row -> print the title only instead of failing the whole report
                        string resultValue = dtFreeTextReport.Rows.Count > 0 ? dtFreeTextReport.Rows[0]["ResultValue"]?.ToString() ?? "" : "";

                        // tables inside the result get borders; the result always sits inside a full-width cell
                        // (it used to be appended straight into <tr>, which pushed it to the top of the page)
                        if (resultValue.IndexOf("<table", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            resultValue = StyleHtmlTables(resultValue,
                                "border-collapse:collapse; border:1px solid #000; width:100%;",
                                "border:1px solid #000; padding:4px;",
                                "border:1px solid #000; padding:4px; font-weight:bold;");
                        }

                        sb.Append("<tr><td colspan='5' style='padding-left:15px;' class='resultTd'>" + resultValue + "</td></tr>");

                        string investigationComment = dtFreeTextReport.Rows.Count > 0 && dtFreeTextReport.Columns.Contains("InvestigationComment")
                            ? dtFreeTextReport.Rows[0]["InvestigationComment"]?.ToString() ?? ""
                            : "";
                        if (!string.IsNullOrEmpty(investigationComment))
                        {
                            AppendRemarks(sb, investigationComment);
                        }
                    }
                    //---merge here Al
                    else if (drRunning["ReportTypeId"].ToString() == "6")//Allergy Report
                    {

                        DataTable dtAllergyReport = _patientLabReport.GetPatientAllergyReportForPrint(Utility.getInt(dr["PatientInvestigationId"]), dummyMode);

                        string txtAllergyType = "";
                        int boxCount = 0; // Counter to track how many boxes have been added to the current row.

                        foreach (DataRow resultRow in dtAllergyReport.Rows)
                        {
                            // Check if we need to create a new section for a different AllergyType
                            if (txtAllergyType != resultRow["AllergyTypeName"].ToString())
                            {
                                // Close the previous row if any, and reset the counter.
                                if (boxCount > 0)
                                {
                                    sb.Append("</tr>");
                                    boxCount = 0;
                                }

                                // Start a new AllergyType section
                                sb.Append("<tr class='resultTable'><td colspan='4' style='text-align:center;font-size:18px;font-weight:bold;background-color:rgba(204, 204, 204, 0.25);'> " + resultRow["AllergyTypeName"].ToString().ToUpper() + " </td></tr>");

                                // Start a new row for the first set of 4 boxes
                                sb.Append("<tr class='resultTable'>");
                            }
                            var allergyImagePath = "";
                            if (!String.IsNullOrEmpty(resultRow["AllergyImagePath"].ToString()))
                                allergyImagePath = resultRow["AllergyImagePath"].ToString().Replace("-", "\\");

                            // Append a result box for the current AllergySubType
                            sb.Append("<td class='resultTd' style='border: 1px dashed gray;'>");
                            sb.Append("<table style='height: 100%; width: 100%;'>");
                            sb.Append("<tr>");
                            sb.Append("<td style='border: 1px solid gray;width: 100%;height: 100%;' class='resultTd'>");
                            sb.Append("<table style='height: 100%; width: 100%;'>");
                            sb.Append("<tr>");
                            sb.Append("<td style='border: 1px dashed lightgray;width: 100%;height: 120px;color: lightgray;' class='resultTd'> <img src=" + allergyImagePath + " alt='Allergy Image' style='width: 100px; height: 100px;'></ td>");
                            sb.Append("</tr>");
                            sb.Append("<tr>");
                            sb.Append("<td style='border: 1px dashed lightgray;width: 100%;height: 40px;font-size:12px;' class='resultTd'>" + resultRow["AllergySubTypeName"].ToString() + "</td>");
                            sb.Append("</tr>");
                            sb.Append("</table>");
                            sb.Append("</td>");
                            sb.Append("<td style='border: 1px solid gray;width: 100%;height: 100%;font-size:15px;' class='resultTd'>Result : " + resultRow["ResultValue"].ToString() + "</td>");
                            sb.Append("</tr>");
                            sb.Append("</table>");
                            sb.Append("</td>");

                            boxCount++;

                            // If 4 boxes have been added, close the current row and prepare for the next one.
                            if (boxCount == 4)
                            {
                                sb.Append("</tr>");
                                boxCount = 0; // Reset the counter for the next set of boxes.
                            }

                            // Update the allergy type to the current one
                            txtAllergyType = resultRow["AllergyTypeName"].ToString();
                        }

                        // Close any open row if fewer than 4 boxes were appended in the last row
                        if (boxCount > 0)
                        {
                            sb.Append("</tr>");
                        }

                    }
                    else if (drRunning["ReportTypeId"].ToString() == "4")//Histo Report

                    {
                        DataTable dtHistoReport = _patientLabReport.GetPatientHistoReportForPrint(Utility.getInt(dr["PatientInvestigationId"]), dummyMode);
                        AppendInvestigationTitle(sb, dr);


                        sb.Append(" <tr style='margin-top:25px;font-size: 14px;'> ");
                        sb.Append("<td colspan='1' style='font-weight: bold;font-size:16px;vertical-align: top;width:17%;'>Specimen :</td> ");
                        sb.Append("<td colspan='4' class='resultTd'>" + dtHistoReport.Rows[0]["SpecimenName"].ToString() + "</td> ");
                        sb.Append(" </tr>");

                        sb.Append(" <tr style='margin-top:25px;font-size: 14px;'> ");
                        sb.Append("<td colspan='1' style='font-weight: bold;font-size:16px;vertical-align: top;width:17%;'>Gross :</td> ");
                        sb.Append("<td colspan='4'  class='resultTd'>" + dtHistoReport.Rows[0]["ResultValueGross"].ToString() + "</td> ");
                        sb.Append(" </tr>");

                        sb.Append(" <tr style='margin-top:25px;font-size: 14px;'> ");
                        sb.Append("<td colspan='1' style='font-weight: bold;font-size:16px;vertical-align: top;width:17%;'>Microscopic :</td> ");
                        sb.Append("<td colspan='4'  class='resultTd'>" + dtHistoReport.Rows[0]["ResultValueMicroscopic"].ToString() + "</td> ");
                        sb.Append(" </tr>");

                        sb.Append(" <tr style='margin-top:25px;font-size: 14px;'> ");
                        sb.Append("<td colspan='1' style='font-weight: bold;font-size:16px;vertical-align: top;width:17%;'>Impression :</td> ");
                        sb.Append("<td colspan='4'  class='resultTd'>" + dtHistoReport.Rows[0]["ResultValueImpression"].ToString() + "</td> ");
                        sb.Append(" </tr>");
                    }
                    else if (drRunning["ReportTypeId"].ToString() == "5") // Micro Report
                    {
                        DataTable dtMicroReport = _patientLabReport.GetPatientMicroReportForPrint(Utility.getInt(dr["PatientInvestigationId"]), dummyMode);

                        // ================= Investigation Name =================
                        AppendInvestigationTitle(sb, dr);

                        // ================= Culture Section =================
                        if (!string.IsNullOrEmpty(dtMicroReport.Rows[0]["ResultValueCulture"].ToString()))
                        {
                            string cultureContent = dtMicroReport.Rows[0]["ResultValueCulture"].ToString();

                            if (cultureContent.Contains("<table"))
                            {
                                cultureContent = StyleHtmlTables(cultureContent,
                                    "width:100%; border:1px solid black; border-collapse:collapse;",
                                    "border:1px solid black; padding:5px;",
                                    "border:1px solid black; padding:5px; background-color:lightgray;");
                            }

                            sb.Append(" <tr> ");
                            sb.Append("<td colspan='1' style='font-weight: bold;font-size:16px;vertical-align: top;width:17%;'>Culture :</td> ");
                            sb.Append(" </tr>");

                            sb.Append(" <tr>");
                            sb.Append("<td colspan='4' class='resultTd'>" + cultureContent + "</td> ");
                            sb.Append(" </tr>");
                        }

                        // Check if we have multiple organisms using traditional loops
                        bool hasMultipleOrganisms = false;
                        if (dtMicroReport.Columns.Contains("OrganismId"))
                        {
                            System.Collections.ArrayList organismIds = new System.Collections.ArrayList();
                            foreach (System.Data.DataRow row in dtMicroReport.Rows)
                            {
                                if (row["OrganismId"] != DBNull.Value && !string.IsNullOrEmpty(row["OrganismId"].ToString()) && Utility.getInt(row["OrganismId"]) > 0)
                                {
                                    string organismId = row["OrganismId"].ToString();
                                    if (!organismIds.Contains(organismId))
                                    {
                                        organismIds.Add(organismId);
                                    }
                                }
                            }
                            hasMultipleOrganisms = organismIds.Count > 1;
                        }

                        // Check if any valid organism exists (OrganismId > 0)
                        bool hasValidOrganism = false;
                        if (dtMicroReport.Columns.Contains("OrganismId"))
                        {
                            foreach (System.Data.DataRow row in dtMicroReport.Rows)
                            {
                                if (row["OrganismId"] != DBNull.Value && !string.IsNullOrEmpty(row["OrganismId"].ToString()) && Utility.getInt(row["OrganismId"]) > 0)
                                {
                                    hasValidOrganism = true;
                                    break;
                                }
                            }
                        }

                        if (hasValidOrganism)
                        {
                            if (hasMultipleOrganisms)
                            {
                                // Multiple organisms - group by OrganismId using traditional approach
                                System.Collections.Hashtable organismGroups = new System.Collections.Hashtable();

                                // Group rows by OrganismId (only where OrganismId > 0)
                                foreach (System.Data.DataRow row in dtMicroReport.Rows)
                                {
                                    if (row["OrganismId"] != DBNull.Value && !string.IsNullOrEmpty(row["OrganismId"].ToString()) && Utility.getInt(row["OrganismId"]) > 0)
                                    {
                                        string organismId = row["OrganismId"].ToString();
                                        if (!organismGroups.ContainsKey(organismId))
                                        {
                                            organismGroups[organismId] = new System.Collections.ArrayList();
                                        }
                                        ((System.Collections.ArrayList)organismGroups[organismId]).Add(row);
                                    }
                                }

                                // Process each organism group
                                bool isFirstOrganism = true;
                                foreach (string organismId in organismGroups.Keys)
                                {
                                    System.Collections.ArrayList organismRows = (System.Collections.ArrayList)organismGroups[organismId];
                                    System.Data.DataRow firstRow = (System.Data.DataRow)organismRows[0];
                                    string organismName = firstRow["OrganismName1"].ToString();

                                    // Add spacing between organism groups (except for first one)
                                    if (!isFirstOrganism)
                                    {
                                        sb.Append("<tr><td colspan='5' style='height: 20px;'></td></tr>");
                                    }

                                    // ================= Organism Name =================
                                    sb.Append("<tr>");
                                    sb.Append("<td colspan='1' style='font-weight: bold;font-size:16px;vertical-align: top;width:18%;'>Organism Name :</td>");
                                    sb.Append("<td colspan='4' class='resultTd' style='font-weight: bold;'>" + organismName + "</td>");
                                    sb.Append("</tr>");

                                    // ================= Antibiotic Table =================
                                    sb.Append("<tr><td colspan='5' style='padding:0;'>");
                                    sb.Append("<table border='1' cellspacing='0' cellpadding='6' style='width:100%; border-collapse:collapse;font-size:14px;border:1px solid black;'>");

                                    // Header row
                                    sb.Append("<tr style='font-weight:bold;text-align:center;background-color:#f2f2f2;'>");
                                    sb.Append("<td style='width:40%;border:1px solid black;padding:4px;'>Antibiotic</td>");
                                    sb.Append("<td style='width:30%;border:1px solid black;padding:4px;'>Interpretation</td>");
                                    sb.Append("<td style='width:30%;border:1px solid black;padding:4px;'>MIC</td>");
                                    sb.Append("</tr>");

                                    // Loop through Antibiotics for this organism
                                    foreach (System.Data.DataRow drAb in organismRows)
                                    {
                                        string interpretation = drAb["Interpretation"].ToString();
                                        string interpretationStyle = "";

                                        // Add color coding for interpretation
                                        if (interpretation.ToLower().Contains("sensitive"))
                                            interpretationStyle = "style='color:green; font-weight:bold;'";
                                        else if (interpretation.ToLower().Contains("resistant"))
                                            interpretationStyle = "style='color:red; font-weight:bold;'";

                                        sb.Append("<tr>");
                                        sb.Append("<td style='border:1px solid black;padding:4px;'>" + drAb["Antibiotic"].ToString() + "</td>");
                                        sb.Append("<td style='border:1px solid black;padding:4px;' " + interpretationStyle + ">" + interpretation + "</td>");
                                        sb.Append("<td style='border:1px solid black;padding:4px;'>" + drAb["MIC"].ToString() + "</td>");
                                        sb.Append("</tr>");
                                    }


                                    sb.Append("</table>");
                                    sb.Append("</td></tr>");

                                    isFirstOrganism = false;
                                }
                            }
                            else
                            {
                                // Single organism (only where OrganismId > 0)
                                // ================= Organism Name =================
                                sb.Append("<tr>");
                                sb.Append("<td colspan='1' style='font-weight: bold;font-size:16px;vertical-align: top;width:18%;'>Organism Name :</td>");
                                sb.Append("<td colspan='4' class='resultTd' style='font-weight: bold;'>" + dtMicroReport.Rows[0]["OrganismName1"].ToString() + "</td>");
                                sb.Append("</tr>");

                                // ================= Antibiotic Table =================
                                sb.Append("<tr><td colspan='5' style='padding:0;'>");
                                sb.Append("<table border='1' cellspacing='0' cellpadding='6' style='width:100%; border-collapse:collapse;font-size:14px;border:1px solid black;'>");

                                // Header row
                                sb.Append("<tr style='font-weight:bold;text-align:center;background-color:#f2f2f2;'>");
                                sb.Append("<td style='width:40%;border:1px solid black;padding:4px;'>Antibiotic</td>");
                                sb.Append("<td style='width:30%;border:1px solid black;padding:4px;'>Interpretation</td>");
                                sb.Append("<td style='width:30%;border:1px solid black;padding:4px;'>MIC</td>");
                                sb.Append("</tr>");

                                // Loop through Antibiotics (only where OrganismId > 0)
                                foreach (System.Data.DataRow drAb in dtMicroReport.Rows)
                                {
                                    if (drAb["OrganismId"] != DBNull.Value && !string.IsNullOrEmpty(drAb["OrganismId"].ToString()) && Utility.getInt(drAb["OrganismId"]) > 0)
                                    {
                                        string interpretation = drAb["Interpretation"].ToString();
                                        string interpretationStyle = "";

                                        // Add color coding for interpretation
                                        if (interpretation.ToLower().Contains("sensitive"))
                                            interpretationStyle = "style='color:green; font-weight:bold;'";
                                        else if (interpretation.ToLower().Contains("resistant"))
                                            interpretationStyle = "style='color:red; font-weight:bold;'";

                                        sb.Append("<tr>");
                                        sb.Append("<td style='border:1px solid black;padding:4px;'>" + drAb["Antibiotic"].ToString() + "</td>");
                                        sb.Append("<td style='border:1px solid black;padding:4px;' " + interpretationStyle + ">" + interpretation + "</td>");
                                        sb.Append("<td style='border:1px solid black;padding:4px;'>" + drAb["MIC"].ToString() + "</td>");
                                        sb.Append("</tr>");
                                    }
                                }

                                sb.Append("</table>");
                                sb.Append("</td></tr>");
                            }
                        }

                        // Add space before additional sections
                        sb.Append("<tr><td colspan='5' style='height: 25px;'></td></tr>");

                        // ================= Notes =================
                        if (!string.IsNullOrEmpty(dtMicroReport.Rows[0]["ResultValueNotes"].ToString()))
                        {
                            sb.Append(" <tr style='margin-top:10px;font-size: 14px;'> ");
                            sb.Append("<td colspan='1' style='font-weight: bold;font-size:16px;vertical-align: top;width:17%;'>Notes :</td> ");
                            sb.Append("<td colspan='4' class='resultTd'>" + dtMicroReport.Rows[0]["ResultValueNotes"].ToString() + "</td> ");
                            sb.Append(" </tr>");
                        }

                        // ================= Colony Count =================
                        if (!string.IsNullOrEmpty(dtMicroReport.Rows[0]["ColonyCount"].ToString()))
                        {
                            sb.Append(" <tr style='margin-top:10px;font-size: 14px;'> ");
                            sb.Append("<td colspan='1' style='font-weight: bold;font-size:16px;vertical-align: top;width:17%;'>Colony Count :</td> ");
                            sb.Append("<td colspan='4' class='resultTd'>" + dtMicroReport.Rows[0]["ColonyCount"].ToString() + "</td> ");
                            sb.Append(" </tr>");
                        }
                    }

                    preDept = drRunning["Department"].ToString();
                    preLabNo = dr["LabNo"].ToString();
                    preIsPrintAlone = Utility.getInt(dr["IsPrintAlone"]);
                    preInvestigationId = Utility.getInt(dr["InvestigationId"]);
                    isFirstRow = false;
                    drPrevious = drRunning;
                }

                sb.Append("</table>");

                string remarks = drRunning["ReportFooterRemarks"].ToString();
                string[] remarksArray = remarks.Split('#');
                if (remarksArray.Length > 0 && !string.IsNullOrWhiteSpace(remarksArray[0]))
                {
                    sb.Append("<div style='font-size: 14px; margin-top: 10px; padding: 4px; background-color: #f8f9fa; border-radius: 4px; font-family: \"Times New Roman\", Times, serif;'>");
                    foreach (string remark in remarksArray)
                    {
                        if (!string.IsNullOrWhiteSpace(remark))
                        {
                            string[] parts = remark.Split('@');
                            string remarkText = parts[0].Trim();
                            int investigationId = parts.Length > 1 && int.TryParse(parts[1], out int id) ? id : -1;

                            if (investigationId == 0)
                            {
                                sb.Append("<div style='margin-bottom: 5px;'>" + remarkText + "</div>");
                            }
                            if (investigationId == Utility.getInt(drRunning["InvestigationId"]))
                            {
                                sb.Append("<div style='margin-bottom: 5px;'>" + remarkText + "</div>");
                            }
                        }
                    }
                    sb.Append("</div>");
                }

                if (ShowEndOfReport)
                    sb.Append("<div style='text-align: center; font-size: " + (BaseFontSize + 1) + "px; font-weight: bold; padding: 4px; color: #000000;'>************* End of Report *************</div>");

                html1LayoutInfo = null;
                AddContent(sb.ToString());
                sb.Clear();
            }
            

            byte[] pdfBuffer = document.WriteToMemory();
            document.Close();

            foreach (var item in pdfSequence)
            {
                if (File.Exists(item.Item3))
                {
                    pdfBuffer = MergeWithExternalPdf(pdfBuffer, item.Item3);
                }
            }


            return pdfBuffer;
        }

        // ───────────────────────── report body helpers (driven by print settings) ─────────────────────────

        /// <summary>CSS for the report body built from font / colour / spacing settings.</summary>
        private string BuildStyles()
        {
            string font = CssFontFamily(FontFamily);
            string grid = ShowGridLines ? $"border-bottom: 1px solid {GridLineColor}; border-right: 1px solid {GridLineColor};" : "";
            int smallFont = Math.Max(BaseFontSize - 1, 6);

            var css = new StringBuilder();
            css.Append("<style>");
            css.Append($"body {{ font-family: {font}; margin: 0; padding: 0; font-size: {BaseFontSize}px; }} ");
            css.Append("table { width: 100%; border-collapse: collapse; margin: 0; } ");
            css.Append($"th, td {{ border: none; padding: 2px; text-align: left; vertical-align: top; font-size: {BaseFontSize}px; font-family: {font}; }} ");
            css.Append($".deptName td {{ text-align: center; font-size: {InvestigationNameFontSize + 2}px; font-weight: bold; padding: 5px; }} ");
            // coloured bar: test name (left) + sample type (right)
            css.Append($".invBand td {{ background-color: {ThemeColor}; color: {ThemeTextColor}; font-weight: bold; padding: 5px 10px; vertical-align: middle; }} ");
            css.Append($".invBand td.invName {{ font-size: {InvestigationNameFontSize}px; }} ");
            css.Append($".invBand td.invSample {{ font-size: {smallFont}px; text-align: right; }} ");
            // accession / collected / received / approved line
            // one line, slightly smaller so all 4 items fit the page width
            css.Append($".accInfo td {{ font-size: {Math.Max(BaseFontSize - 2, 6)}px; padding: 4px 4px; white-space: nowrap; }} ");
            // column header (Observation | Result | Unit | Ref. Interval | Method)
            css.Append($".colHeader td {{ background-color: {TableHeaderBgColor}; color: {TableHeaderTextColor}; font-weight: bold; font-size: {TableHeaderFontSize}px; padding: {RowSpacing + 2}px 8px; }} ");
            css.Append($".investigationName {{ text-align: left; font-size: {InvestigationNameFontSize}px; font-weight: bold; padding: 3px 0; }} ");
            css.Append($".sampleInfo {{ text-align: left; font-size: {smallFont}px; padding-left: 15px; color: #555; }} ");
            // result rows: RowSpacing = space between two rows
            css.Append($".resultRow td {{ padding: {RowSpacing}px 8px; font-size: {BaseFontSize}px; {grid} }} ");
            css.Append(".resultRow td.lastCell { border-right: none; } ");
            css.Append($".resultRow td.abnormal {{ color: {AbnormalResultColor}; }} ");
            // fixed columns: wrap long methods instead of pushing the table past the page edge
            css.Append($".resultRow td.observation-method, .observation-method {{ font-size: {MethodFontSize}px; }} ");
            css.Append(".invTable { width: 100%; table-layout: fixed; border-collapse: collapse; } ");
            css.Append(".invTable td { word-wrap: break-word; } ");
            css.Append(".colHeader td { white-space: nowrap; overflow: hidden; } ");
            // never split a single result row or the test title block across two pages
            css.Append("tr.keepTogether, tr.resultRow, tr.colHeader { page-break-inside: avoid; } ");
            css.Append(".obsComment td { padding-left: 20px; padding-bottom: 5px; font-style: italic; color: #555; } ");
            css.Append($".resultTable {{ font-size: {BaseFontSize}px; line-height: {BaseFontSize + 2}px; }} ");
            css.Append(".resultTd { padding: 2px; } ");
            css.Append(".no-border { border: none !important; } ");
            css.Append("</style>");
            return css.ToString();
        }

        /// <summary>"Arial, Times New Roman" -> "'Arial', 'Times New Roman', sans-serif"</summary>
        private static string CssFontFamily(string font)
        {
            var generic = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "serif", "sans-serif", "monospace", "cursive", "fantasy" };
            var parts = new List<string>();
            bool hasGeneric = false;
            foreach (string p in (font ?? "").Split(','))
            {
                string name = p.Replace("'", "").Replace("\"", "").Trim();
                if (name.Length == 0) continue;
                if (generic.Contains(name)) { parts.Add(name); hasGeneric = true; }
                else parts.Add("'" + name + "'");
            }
            if (parts.Count == 0) parts.Add("'Arial'");
            if (!hasGeneric) parts.Add("sans-serif");
            return string.Join(", ", parts);
        }

        private string NablImageHtml(int height)
        {
            string path;
            if (!string.IsNullOrEmpty(drRunning["NABLFilePath"].ToString()))
                path = drRunning["NABLFilePath"].ToString().Replace("-", "\\");
            else if (Utility.getInt(drRunning["IsDefaultLogo"]) == 1)
                path = defaultNABLLogoPath;
            else
                return string.Empty;

            return $"<img src='{path}' style='height: {height}px; margin-left: 10px; margin-right: 10px; vertical-align: middle;' />";
        }

        /// <summary>
        /// Test name: coloured bar (+ accession line, + optional column header) when ShowInvestigationBand,
        /// otherwise the old plain bold row. In the bar layout everything is one row with
        /// page-break-inside: avoid, so the title block never splits from its column header.
        /// </summary>
        private void AppendInvestigationTitle(StringBuilder sb, DataRow dr, bool withColumnHeader = false, string firstResultRowHtml = "")
        {
            string name = NablImageHtml(25) + dr["InvestigationName"].ToString();

            if (!ShowInvestigationBand)
            {
                sb.Append("<tr><td colspan='5' class='investigationName'>" + name + "</td></tr>");
                return;
            }

            string sample = string.Empty;
            if (ShowSampleType)
            {
                sample = dr["SampleType"].ToString().Trim();
                if (sample.Length > 0 && !sample.EndsWith("sample", StringComparison.OrdinalIgnoreCase))
                    sample += " Sample";
            }

            sb.Append($"<tr class='keepTogether'><td colspan='5' style='padding: {InvestigationSpacing}px 0 0 0;'>");
            sb.Append("<table class='invBand'><tr>");
            sb.Append("<td class='invName'>" + name + "</td>");
            sb.Append("<td class='invSample'>" + sample + "</td>");
            sb.Append("</tr></table>");

            if (ShowAccessionInfo)
                sb.Append(AccessionInfoTable(dr));

            if (withColumnHeader)
            {
                // same fixed column widths as the result table below, so the columns line up
                sb.Append("<table class='invTable'>" + InvestigationColGroup());
                AppendColumnHeader(sb);
                sb.Append(firstResultRowHtml);
                sb.Append("</table>");
            }

            sb.Append("</td></tr>");
        }

        /// <summary>Accession No | Collected On | Received On | Approved On (empty values are skipped).</summary>
        private string AccessionInfoTable(DataRow dr)
        {
            var cells = new StringBuilder();
            void Add(string label, string column)
            {
                if (!dr.Table.Columns.Contains(column)) return;
                string value = dr[column].ToString().Trim();
                if (!string.IsNullOrWhiteSpace(value) && value != "0")
                    cells.Append($"<td><b>{label}:</b> {value}</td>");
            }

            Add("Accession No", "BarCode");
            Add("Collected On", "InvSampleColDateTime");
            Add("Received On", "InvDeptRecDateTime");
            Add("Approved On", "InvReportApprovalDateTime");

            return cells.Length == 0 ? string.Empty : "<table class='accInfo'><tr>" + cells + "</tr></table>";
        }

        /// <summary>Fixed column widths shared by the column header and the result rows of a test.</summary>
        private static string InvestigationColGroup()
        {
            return "<colgroup><col style='width: 30%;'/><col style='width: 11%;'/><col style='width: 12%;'/>"
                 + "<col style='width: 24%;'/><col style='width: 23%;'/></colgroup>";
        }

        private void AppendColumnHeader(StringBuilder sb)
        {
            sb.Append("<tr class='colHeader'>");
            sb.Append("<td>Observation</td>");
            sb.Append("<td>Result</td>");
            sb.Append("<td>Unit</td>");
            if (ShowMethodColumn)
            {
                sb.Append("<td>Biological Ref. Interval</td>");
                sb.Append("<td>Method</td>");
            }
            else
            {
                sb.Append("<td colspan='2'>Biological Ref. Interval</td>");
            }
            sb.Append("</tr>");
        }

        /// <summary>
        /// Adds border styles to &lt;table&gt;, &lt;td&gt; and &lt;th&gt; tags of stored HTML.
        /// Uses whole-tag matching, so &lt;thead&gt; / &lt;tbody&gt; are not broken (plain Replace("&lt;th") turned &lt;thead&gt; into garbage).
        /// </summary>
        private static string StyleHtmlTables(string html, string tableStyle, string cellStyle, string headCellStyle)
        {
            if (string.IsNullOrEmpty(html)) return html;
            var opts = System.Text.RegularExpressions.RegexOptions.IgnoreCase;
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<table\b", "<table style=\"" + tableStyle + "\"", opts);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<td\b", "<td style=\"" + cellStyle + "\"", opts);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<th\b", "<th style=\"" + headCellStyle + "\"", opts);
            return html;
        }

        private static bool IsAbnormal(DataRow r)
        {
            if (!decimal.TryParse(r["ResultValue"].ToString(), out decimal value) ||
                !decimal.TryParse(r["MinValue"].ToString(), out decimal min) ||
                !decimal.TryParse(r["MaxValue"].ToString(), out decimal max))
                return false;
            return value < min || value > max;
        }

        private string ResultCell(DataRow r, string bg, int colspan, bool isLastCell)
        {
            bool abnormal = IsAbnormal(r);
            bool bold = r["IsResultBold"].ToString() == "1" || (abnormal && ResultBoldIfAbnormal);

            string cls = ((abnormal ? "abnormal " : "") + (isLastCell ? "lastCell" : "")).Trim();
            string span = colspan > 1 ? $" colspan='{colspan}'" : "";
            return $"<td{span} class='{cls}' style='{(bold ? "font-weight: bold;" : "")}{bg}'>{r["ResultValue"]}</td>";
        }

        /// <summary>One observation row (+ its comment row). Striping / bold / method column follow print settings.</summary>
        private void AppendResultRow(StringBuilder sb, DataRow r, bool singleTestMode)
        {
            string bg = ShowStripedRows && stripeIndex % 2 == 1 ? $" background-color: {AltRowColor};" : "";
            stripeIndex++;

            string name = r["ObservationName"].ToString();
            bool isBold = r["IsBold"].ToString() == "True";
            bool isUnderline = r["IsUnderLine"].ToString() == "True";
            string method = r["ObservationMethod"].ToString().Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            string methodCell = $"<td class='observation-method lastCell' style='{bg}'>{method}</td>";

            sb.Append("<tr class='resultRow'>");

            if (!singleTestMode && r["IsHeader"].ToString() == "1")
            {
                // sub heading, e.g. "Differential Leucocyte Count (DLC)"
                string style = (isBold ? "font-weight: bold;" : "") + (isUnderline ? " text-decoration: underline;" : "");
                if (ShowMethodColumn)
                {
                    sb.Append($"<td colspan='4' style='{style}{bg}'>{name}</td>");
                    sb.Append(methodCell);
                }
                else
                {
                    sb.Append($"<td colspan='5' class='lastCell' style='{style}{bg}'>{name}</td>");
                }
            }
            else
            {
                bool nameBold = singleTestMode || isBold || ObservationNameBold;
                string nameStyle = (nameBold ? "font-weight: bold;" : "") + (!singleTestMode && isUnderline ? " text-decoration: underline;" : "");
                sb.Append($"<td style='{nameStyle}{bg}'>{name}</td>");

                if (r["FieldTypeId"].ToString() == "1")
                {
                    sb.Append(ResultCell(r, bg, 1, false));
                    sb.Append($"<td style='{bg}'>{r["Unit"]}</td>");
                    if (ShowMethodColumn)
                    {
                        sb.Append($"<td style='{bg}'>{r["DisplayRange"]}</td>");
                        sb.Append(methodCell);
                    }
                    else
                    {
                        sb.Append($"<td colspan='2' class='lastCell' style='{bg}'>{r["DisplayRange"]}</td>");
                    }
                }
                else
                {
                    sb.Append(ResultCell(r, bg, 4, true));
                }
            }

            sb.Append("</tr>");

            string comment = r["ObservationComment"].ToString();
            if (!string.IsNullOrEmpty(comment))
                sb.Append($"<tr class='obsComment'><td colspan='5'>{comment}</td></tr>");
        }

        /// <summary>Investigation comment: dashed "Remarks" block in the new layout, grey box in the old one.</summary>
        private void AppendRemarks(StringBuilder sb, string remarks)
        {
            int smallFont = Math.Max(BaseFontSize - 1, 6);
            sb.Append("<tr>");
            if (ShowInvestigationBand)
            {
                sb.Append($"<td colspan='5' style='font-size: {smallFont}px; padding: 6px 4px; border-top: 1px dashed #999; border-bottom: 1px dashed #999;'>");
                sb.Append("<b>Comments :</b> ");
            }
            else
            {
                sb.Append($"<td colspan='5' style='font-size: {smallFont}px; padding: 5px; background-color: #f8f9fa;'>");
                sb.Append("<strong>Comments :-</strong><br/>");
            }
            sb.Append(remarks);
            sb.Append("</td></tr>");
        }

        private void AddContent(string Content)
        {
            PdfPage page1 = document.AddPage(PdfPageSize.A4, new PdfDocumentMargins(5), PdfPageOrientation.Portrait);
            PdfHtml html1 = new PdfHtml();

            if (html1LayoutInfo == null)
            {
                html1LayoutInfo = page1.Layout(html1);
            }

            html1 = new PdfHtml(MarginLeft, html1LayoutInfo.LastPageRectangle.Height, PageWidth, Content, null);
            html1.PageCreatingEvent += new PdfPageCreatingDelegate(htmlToPdfConverter_PageCreatingEvent);
            html1.FontEmbedding = false;
            html1.BrowserWidth = BrowserWidth;

            html1LayoutInfo = page1.Layout(html1);
        }

        void htmlToPdfConverter_PageCreatingEvent(PdfPageCreatingParams eventParams)
        {
            PdfPage page1 = eventParams.PdfPage;
            SetHeader(page1);
            string downloadUrl = GenerateReportDownloadUrl();
            SetFooter(page1, downloadUrl);
        }

        private void SetHeader(PdfPage page)
        {
            // create the document header
            //if (page.Index == 0)
            //{

            page.CreateHeaderCanvas(HeaderHeight);

            // layout HTML in header
            // PdfHtml headerHtml = null;
            // if (headerHtml == null)
            resolvePatientReportHeader();

            PdfHtml headerHtml = new PdfHtml(XHeader, YHeader, PageWidth, patientHeaderString, null);
            //headerHtml.FitDestHeight = true;
            headerHtml.FitDestWidth = true;
            headerHtml.FontEmbedding = false;
            headerHtml.BrowserWidth = HeaderBrowserWidth;

            page.Header.Layout(headerHtml);

            //}
            //else
            //{
            //    HeaderHeight = 100;
            //    page.CreateHeaderCanvas(HeaderHeight);
            //}




            if (BottomLine)
            {
                // create a border for header
                float headerHeight = page.Header.Height;
                float headerWidth = page.Header.Width;

                System.Drawing.Font pageNumberingFont = new System.Drawing.Font(new System.Drawing.FontFamily("Times New Roman"), 12, System.Drawing.GraphicsUnit.Point);

                PdfText headerHtmlSpecimen = new PdfText(XHeader, headerHeight - 20, PageWidth, "", pageNumberingFont);

                page.Header.Layout(headerHtmlSpecimen);


                // PdfRectangle borderRectangle = new PdfRectangle(XHeader, HeaderHeight - 1, PageWidth, 0.25f);

                //borderRectangle.LineStyle.LineWidth = 0.5f;
                //borderRectangle.ForeColor = Color.Black;
                //page.Header.Layout(borderRectangle);
            }


            if (TopLine)
            {
                // create a border for header
                float headerHeight = page.Header.Height;
                float headerWidth = page.Header.Width;
                PdfRectangle borderRectangle = new PdfRectangle(XHeader, YHeader, PageWidth, 0.25f);

                borderRectangle.LineStyle.LineWidth = 0.5f;
                borderRectangle.ForeColor = Color.Black;
                page.Header.Layout(borderRectangle);
            }

            string letterHeadPath = isHeaderPNG == 1 ? ResolveImagePath(drRunning["LetterHeadFilePath"]?.ToString()) : string.Empty;
            if (!String.IsNullOrEmpty(letterHeadPath))
            {
                page.Layout(getPDFImageWaterMark(
                    -200 + float.Parse(drRunning["PaddingLeft"].ToString()),
                    -410 + float.Parse(drRunning["PaddingTop"].ToString()),
                    385 + float.Parse(drRunning["PaddingRight"].ToString()),
                    letterHeadPath
                ));
            }
        }

        private void SetFooter(PdfPage page, string downloadUrl)
        {
            // create the document Foooter
            page.CreateFooterCanvas(FooterHeight);
            float footerHeight = page.Footer.Height;
            float footerWidth = page.Footer.Width;
            // create a border for footer
            if (footerTopLine)
            {
                PdfRectangle borderRectangle = new PdfRectangle(XFooter, 0.5f, PageWidth, 0.25f);
                borderRectangle.LineStyle.LineWidth = 0.5f;
                borderRectangle.ForeColor = Color.Black;
                page.Footer.Layout(borderRectangle);
            }




            if (QRCode)
            {
                if (QRCodePosition == 1)
                    page.Header.Layout(getPDFImageforQRcode(XQRcode, YQRcode, downloadUrl));
                else
                    page.Footer.Layout(getPDFImageforQRcode(XQRcode, YQRcode, downloadUrl));
            }

            // Barcode (Code 128 of the Lab No.) - settings: IsActiveBarcode, BarcodePosition (1 = header, else footer),
            // XBarcode / YBarcode, BarcodeWidth / BarcodeHeight, RotateBarcode90Degree (LabReportPrintSettingMaster)
            if (Barcode)
            {
                string labNo = drRunning != null && drRunning.Table.Columns.Contains("LabNo")
                    ? drRunning["LabNo"]?.ToString()?.Trim() ?? string.Empty
                    : string.Empty;

                if (!string.IsNullOrEmpty(labNo))
                {
                    try
                    {
                        var barcodeImage = getPDFImageforBarcode(labNo);
                        if (BarcodePosition == 1)
                            page.Header.Layout(barcodeImage);
                        else
                            page.Footer.Layout(barcodeImage);
                    }
                    catch (Exception ex)
                    {
                        // e.g. width too small for the Lab No. or a character Code 128 cannot encode -> report still prints
                        _log.Warn($"Barcode not printed for LabNo={labNo}: {ex.Message}");
                    }
                }
            }



            resolvePatientReportFooter();
            PdfHtml footerHtml = new PdfHtml(25, FooterHeight - YFooter, PageWidth, patientFooterString, null);
            footerHtml.FitDestWidth = true;
            footerHtml.FontEmbedding = false;
            footerHtml.BrowserWidth = FooterBrowserWidth;
            page.Footer.Layout(footerHtml);

            // Footer text elements
            System.Drawing.Font pageNumberingFont = new System.Drawing.Font(new System.Drawing.FontFamily("Times New Roman"), 8, System.Drawing.GraphicsUnit.Point);

            if (PageNumbering == 1)
            {
                // add page numbering in a text element
                PdfText pageNumberingText = new PdfText(500, FooterHeight - 10, "Page {CrtPage} of {PageCount}", pageNumberingFont);
                page.Footer.Layout(pageNumberingText);
            }

            if (PrintDateTime == 1)
            {
                // print datetime
                PdfText page1Text = new PdfText(10, FooterHeight - 10, String.Format("{0}   {1}", DateTime.Now.ToString("dd/MM/yyyy"), DateTime.Now.ToShortTimeString()), pageNumberingFont);
                page1Text.ForeColor = System.Drawing.Color.Black;
                page.Footer.Layout(page1Text);
            }


            if (PreparedBy == 1)
            {
                // SaveByText
                PdfText page1SaveByText = new PdfText(150, FooterHeight - 10, "Prepared By :", pageNumberingFont);
                page1SaveByText.ForeColor = System.Drawing.Color.Black;
                page.Footer.Layout(page1SaveByText);

                // SaveBy
                PdfText page1SaveBy = new PdfText(200, FooterHeight - 10, Utility.getString(drPrevious["InvResultEntryBy"]), pageNumberingFont);
                page1SaveBy.ForeColor = System.Drawing.Color.Black;
                page.Footer.Layout(page1SaveBy);
            }

            if (PrintedBy == 1)
            {
                // PrintByText
                PdfText page1PrintByText = new PdfText(350, FooterHeight - 10, "Printed By :", pageNumberingFont);
                page1PrintByText.ForeColor = System.Drawing.Color.Black;
                page.Footer.Layout(page1PrintByText);

                //PrintBy
                PdfText page1PrintBy = new PdfText(400, FooterHeight - 10, Utility.getString(drPrevious["InvResultPrintBy"]), pageNumberingFont);
                page1PrintBy.ForeColor = System.Drawing.Color.Black;
                page.Footer.Layout(page1PrintBy);
            }

            //Set Digital Sinature 
            if (page.Footer != null && showDigitalSinature && Utility.getInt(drPrevious["IsReportApproved"]) == 1)
            {
                string storedSignPath = drPrevious["DoctorSignFilePath"]?.ToString() ?? string.Empty;
                string signPath = ResolveImagePath(storedSignPath);
                // missing image file would make HiQPdf throw and fail the whole report -> skip it (and say why)
                if (!String.IsNullOrEmpty(signPath))
                    page.Footer.Layout(getPDFImage(XSignature, YSignature, signPath));
                else if (!String.IsNullOrEmpty(storedSignPath) && page.Index == 0)
                    _log.Warn($"Doctor signature not printed: file not found '{storedSignPath}'");
            }



        }

        private void resolvePatientReportHeader()
        {
            patientHeaderString = headerMasterString;
            for (int i = 0; i < dtPatientInvestigations.Columns.Count; i++)
            {
                string columnName = dtPatientInvestigations.Columns[i].ColumnName;
                patientHeaderString = patientHeaderString.Replace("##" + columnName + "##", drPrevious[columnName].ToString());
            }
        }

        private void resolvePatientReportFooter()
        {
            patientFooterString = footerMasterString;
            for (int i = 0; i < dtPatientInvestigations.Columns.Count; i++)
            {
                string columnName = dtPatientInvestigations.Columns[i].ColumnName;
                patientFooterString = patientFooterString.Replace("##" + columnName + "##", drPrevious[columnName].ToString());
            }
        }

        /// <summary>
        /// Stored image path -> existing file, or "" when there is none.
        /// The path is tried as stored first (e.g. "D:/DMS/API/DoctorSignatures/x.png"); the old
        /// "-" -> "\" conversion is only a fallback, because it breaks file names that contain a hyphen.
        /// </summary>
        private static string ResolveImagePath(string stored)
        {
            if (String.IsNullOrWhiteSpace(stored)) return string.Empty;
            string path = stored.Trim();
            if (File.Exists(path)) return path;
            string legacy = path.Replace("-", "\\");
            return File.Exists(legacy) ? legacy : string.Empty;
        }

        private PdfImage getPDFImage(float X, float Y, string SignImg)
        {
            PdfImage transparentResizedPdfImage = new PdfImage(X, Y, SignImg);
            transparentResizedPdfImage.PreserveAspectRatio = true;
            return transparentResizedPdfImage;
        }

        private PdfImage getPDFImageWaterMark(float X, float Y, float Z, string SignImg)
        {
            PdfImage transparentResizedPdfImage = new PdfImage(X, Y, Z, SignImg);
            transparentResizedPdfImage.PreserveAspectRatio = true;
            return transparentResizedPdfImage;
        }

        private PdfImage getPDFImageforQRcode(float X, float Y, string downloadUrl)
        {
            string base64Image = getQRcode(downloadUrl);
            PdfImage transparentResizedPdfImage = new PdfImage(X, Y, Base64StringToImage(base64Image));
            transparentResizedPdfImage.PreserveAspectRatio = true;
            return transparentResizedPdfImage;
        }





        public System.Drawing.Image Base64StringToImage(string base64String)
        {
            byte[] imageBytes = Convert.FromBase64String(base64String.Replace("data:image/png;base64,", ""));
            MemoryStream memStream = new MemoryStream(imageBytes, 0, imageBytes.Length);

            memStream.Write(imageBytes, 0, imageBytes.Length);
            System.Drawing.Image image = System.Drawing.Image.FromStream(memStream);
            Bitmap newImage = new Bitmap(QRWidth, QRHeight);
            using (Graphics graphics = Graphics.FromImage(newImage))
                graphics.DrawImage(image, 0, 0, QRWidth, QRHeight);
            return newImage;
        }

        public string getQRcode(string code)
        {

            QRCodeEncoder enc = new QRCodeEncoder();
            Bitmap qrcode = enc.Encode(code);
            using (MemoryStream ms = new MemoryStream())
            {
                qrcode.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                byte[] byteImage = ms.ToArray();
                return "data:image/png;base64," + Convert.ToBase64String(byteImage);
            }



        }


        private PdfImage getPDFImageforBarcode(string data)
        {
            Barcode barcodLib = new Barcode();


            Color foreColor = Color.Black; // Color to print barcode
            Color backColor = Color.White; //background color


            Image barcodeImage = barcodLib.Encode(TYPE.CODE128, data, foreColor, backColor, BarCodeWidth, BarCodeHeight);
            if (RotateBarcode90Degree == 1)
            {
                barcodeImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
            }
            PdfImage transparentResizedPdfImage = new PdfImage(XBarcode, YBarcode, barcodeImage);
            transparentResizedPdfImage.PreserveAspectRatio = true;
            return transparentResizedPdfImage;
        }




        //private string GenerateReportDownloadUrl(string encryptedPatientInvestigationIds)
        //{
        //    string baseUrl = HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority);
        //    string path = HttpContext.Current.Request.ApplicationPath;
        //    return $"{baseUrl}{path}/printPatientInvestigationReport.aspx?PTINVID={encryptedPatientInvestigationIds}&download=true&isHeaderPNG=1&isSendEmail={false}&isSendWhatsAppMessage={false}&contacts={patientContacts}&emailIds={patientEmailIds}&branchId={branchId}&userId={userId}";
        //  
        //}


        private string GenerateReportDownloadUrl()
        {
            // QR -> existing print endpoint (LabReportController). No contacts / e-mails in the URL (they would be printed into the QR).
            return $"{_baseUrl}/api/Lab/printPatientInvestigationReport?PatientInvestigationIds={Uri.EscapeDataString(patientInvestigationIds)}&BranchId={branchId}&IsHeaderPng={isHeaderPNG}&Download=true&DummyMode={dummyMode}";
        }


        private byte[] MergeWithExternalPdf(byte[] mainPdfBuffer, string externalPdfPath)
        {
            try
            {
                if (!File.Exists(externalPdfPath))
                {
                    return mainPdfBuffer;
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    using (iTextSharp.text.Document document = new iTextSharp.text.Document())
                    {
                        using (PdfCopy copy = new PdfCopy(document, ms))
                        {
                            document.Open();

                            // Add main PDF
                            PdfReader mainReader = new PdfReader(mainPdfBuffer);
                            for (int i = 1; i <= mainReader.NumberOfPages; i++)
                            {
                                copy.AddPage(copy.GetImportedPage(mainReader, i));
                            }
                            mainReader.Close();

                            // Add external PDF
                            PdfReader externalReader = new PdfReader(externalPdfPath);
                            for (int i = 1; i <= externalReader.NumberOfPages; i++)
                            {
                                copy.AddPage(copy.GetImportedPage(externalReader, i));
                            }
                            externalReader.Close();

                            document.Close();
                        }
                    }

                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PDF Merge Error: " + ex.Message);
                return mainPdfBuffer;
            }
        }

    }
}
