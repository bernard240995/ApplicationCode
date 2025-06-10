using System;
using System.Text.RegularExpressions;
using EDIOrderData;

namespace EDIFACTToSQL
{
    public class EDIParser
    {
        public EDIOrder ParseEDIFACT(string ediContent)
        {
            if (string.IsNullOrWhiteSpace(ediContent))
                throw new ArgumentException("EDI content cannot be null or empty", nameof(ediContent));

            var order = new EDIOrder();
            var segments = ediContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment)) continue;

                var elements = segment.Split('+');
                if (elements.Length == 0) continue;

                try
                {
                    ProcessSegment(elements, order, ediContent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error processing segment '{segment}': {ex.Message}");
                }
            }

            return order;
        }

        private void ProcessSegment(string[] elements, EDIOrder order, string fullEdiContent)
        {
            if (elements == null || elements.Length == 0) return;

            switch (elements[0])
            {
                case "UNH":
                    order.MessageReference = SafeGetElement(elements, 1);
                    break;
                case "BGM":
                    order.DocumentNumber = SafeGetElement(elements, 3);
                    break;
                case "DTM":
                    ProcessDTMSegment(elements, order);
                    break;
                case "NAD":
                    ProcessNADSegment(elements, order, fullEdiContent);
                    break;
                case "LIN":
                    if (elements.Length > 3)
                        order.ItemNumber = SafeGetElement(elements[3].Split(':'), 0);
                    break;
                case "QTY":
                    if (elements.Length > 1 && elements[1].StartsWith("21:"))
                    {
                        order.Quantity = SafeGetElement(elements[1].Split(':'), 1, "0");
                    }
                    break;
                case "PRI":
                    if (elements.Length > 1 && elements[1].StartsWith("AAB:"))
                    {
                        order.UnitPrice = SafeGetElement(elements[1].Split(':'), 1, "0");
                    }
                    break;
                case "MOA":
                    if (elements.Length > 1 && elements[1].StartsWith("203:"))
                    {
                        order.LineAmount = SafeGetElement(elements[1].Split(':'), 1, "0");
                    }
                    break;
            }
        }

        private void ProcessDTMSegment(string[] elements, EDIOrder order)
        {
            if (elements.Length > 1)
            {
                var dtmParts = elements[1].Split(':');
                if (dtmParts.Length > 1)
                {
                    var dateType = dtmParts[0];
                    var dateValue = dtmParts[1];
                    var formattedDate = FormatDate(dateValue);

                    switch (dateType)
                    {
                        case "137":
                            order.DocumentDate = formattedDate ?? string.Empty;
                            break;
                        case "43E":
                            order.DeliveryDate = formattedDate ?? string.Empty;
                            break;
                    }
                }
            }
        }

        private void ProcessNADSegment(string[] elements, EDIOrder order, string fullEdiContent)
        {
            if (elements.Length < 2) return;

            var partyType = elements[1];
            if (partyType == "BY" && elements.Length > 2)
            {
                var buyerParts = elements[2].Split(':');
                order.BuyerId = SafeGetElement(buyerParts, 0);
                order.BuyerName = SafeGetElement(elements, 4)?.Trim() ?? string.Empty;
            }
            else if (partyType == "SF")
            {
                order.SupplierName = SafeGetElement(elements, 3)?.Trim() ?? string.Empty;
                var supplierIdMatch = Regex.Match(fullEdiContent, @"RFF\+IA:(\d+)");
                if (supplierIdMatch.Success)
                {
                    order.SupplierId = supplierIdMatch.Groups[1].Value;
                }
            }
        }

        private string SafeGetElement(string[] elements, int index, string defaultValue = "")
        {
            return elements != null && elements.Length > index ? elements[index] : defaultValue;
        }

        private string FormatDate(string ediDate)
        {
            if (string.IsNullOrWhiteSpace(ediDate)) return null;

            var datePart = ediDate.Length >= 8 ? ediDate.Substring(0, 8) : ediDate;

            if (DateTime.TryParseExact(datePart, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime date))
            {
                return date.ToString("yyyy-MM-dd");
            }
            return null;
        }
    }
}