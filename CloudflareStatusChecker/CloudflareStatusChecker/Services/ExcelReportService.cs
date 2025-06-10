using ClosedXML.Excel;
using CloudflareStatusChecker.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace CloudflareStatusChecker.Services
{
    public class ExcelReportService
    {
        public MemoryStream GenerateReport(IncidentResponse response)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Incidents");

            
            worksheet.Cell(1, 1).Value = "ID";
            worksheet.Cell(1, 2).Value = "Name";
            worksheet.Cell(1, 3).Value = "Status";
            worksheet.Cell(1, 4).Value = "Impact";
            worksheet.Cell(1, 5).Value = "Created At";
            worksheet.Cell(1, 6).Value = "Updated At";
            worksheet.Cell(1, 7).Value = "Resolved At";
            worksheet.Cell(1, 8).Value = "Shortlink";
            worksheet.Cell(1, 9).Value = "Affected Components";
            worksheet.Cell(1, 10).Value = "Latest Update";

            
            var headerRange = worksheet.Range(1, 1, 1, 10);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            int row = 2;
            foreach (var incident in response.Incidents)
            {
                worksheet.Cell(row, 1).Value = incident.Id;
                worksheet.Cell(row, 2).Value = incident.Name;
                worksheet.Cell(row, 3).Value = incident.Status;
                worksheet.Cell(row, 4).Value = incident.Impact;
                worksheet.Cell(row, 5).Value = incident.CreatedAt;
                worksheet.Cell(row, 6).Value = incident.UpdatedAt;
                worksheet.Cell(row, 7).Value = incident.ResolvedAt;
                worksheet.Cell(row, 8).Value = incident.Shortlink;

                
                if (incident.Status?.Equals("resolved", StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    worksheet.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.LightGreen;
                }

                var affectedComponents = string.Join(", ", GetComponentNames(incident));
                worksheet.Cell(row, 9).Value = affectedComponents;
                worksheet.Cell(row, 9).Style.Alignment.WrapText = true;

                if (incident.IncidentUpdates != null && incident.IncidentUpdates.Count > 0)
                {
                    worksheet.Cell(row, 10).Value = incident.IncidentUpdates[0].Body;
                    worksheet.Cell(row, 10).Style.Alignment.WrapText = true;
                }

                
                var dataRange = worksheet.Range(row, 1, row, 10);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                row++;
            }

            
            worksheet.Range(1, 1, 1, 10).SetAutoFilter();

            
            worksheet.Columns().AdjustToContents();
            worksheet.Column(9).Width = 30;
            worksheet.Column(10).Width = 50;

            
            var allDataRange = worksheet.Range(1, 1, row - 1, 10);
            allDataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);
            memoryStream.Position = 0;

            return memoryStream;
        }

        private List<string> GetComponentNames(Incident incident)
        {
            var componentNames = new List<string>();
            if (incident.AffectedComponents != null)
            {
                foreach (var component in incident.AffectedComponents)
                {
                    if (!string.IsNullOrEmpty(component.Name))
                    {
                        componentNames.Add(component.Name);
                    }
                }
            }
            return componentNames;
        }
    }
}