using System.Data;
using System.Data.SqlClient;
using System.IO;
using EDIOrderData;

namespace EDIFACTToSQL
{
    public class EDIDatabaseHandler
    {
        private readonly string _connectionString;

        public EDIDatabaseHandler(string connectionString)
        {
            _connectionString = connectionString;
        }

        public bool IsDuplicateOrder(EDIOrder order)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                const string checkSql = @"
                SELECT COUNT(1) 
                FROM EDIOrderData 
                WHERE DocumentNumber = @DocumentNumber
                AND BuyerId = @BuyerId
                AND ItemNumber = @ItemNumber
                AND Quantity = @Quantity
                AND LineAmount = @LineAmount";

                using var command = new SqlCommand(checkSql, connection);
                AddCommonParameters(command, order);

                var count = (int)command.ExecuteScalar();
                return count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for duplicate order: {ex.Message}");
                throw;
            }
        }

        public void InsertOrder(EDIOrder order, string fileName)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();
                EnsureTableExists(connection);
                InsertOrderData(connection, order, fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting order: {ex.Message}");
                throw;
            }
        }

        private void EnsureTableExists(SqlConnection connection)
        {
            const string createTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EDIOrderData')
            CREATE TABLE EDIOrderData (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                FileName NVARCHAR(255),
                MessageReference NVARCHAR(255),
                DocumentNumber NVARCHAR(255),
                DocumentDate DATE,
                DeliveryDate DATE,
                BuyerId NVARCHAR(255),
                BuyerName NVARCHAR(255),
                SupplierId NVARCHAR(255),
                SupplierName NVARCHAR(255),
                ItemNumber NVARCHAR(255),
                Quantity DECIMAL(18,2),
                UnitPrice DECIMAL(18,4),
                LineAmount DECIMAL(18,2),
                ProcessDate DATETIME DEFAULT GETDATE(),
                CONSTRAINT UQ_EDIOrder UNIQUE (DocumentNumber, BuyerId, ItemNumber, Quantity, LineAmount)
            )";

            using var command = new SqlCommand(createTableSql, connection);
            command.ExecuteNonQuery();
        }

        private void InsertOrderData(SqlConnection connection, EDIOrder order, string fileName)
        {
            const string insertSql = @"
            INSERT INTO EDIOrderData (
                FileName, MessageReference, DocumentNumber, DocumentDate, DeliveryDate,
                BuyerId, BuyerName, SupplierId, SupplierName,
                ItemNumber, Quantity, UnitPrice, LineAmount
            ) VALUES (
                @FileName, @MessageReference, @DocumentNumber, 
                CASE WHEN @DocumentDate = '' THEN NULL ELSE TRY_CONVERT(DATE, @DocumentDate) END,
                CASE WHEN @DeliveryDate = '' THEN NULL ELSE TRY_CONVERT(DATE, @DeliveryDate) END,
                @BuyerId, @BuyerName, @SupplierId, @SupplierName,
                @ItemNumber, @Quantity, @UnitPrice, @LineAmount
            )";

            using var command = new SqlCommand(insertSql, connection);
            AddParametersToCommand(command, order, fileName);
            command.ExecuteNonQuery();
        }

        private void AddParametersToCommand(SqlCommand command, EDIOrder order, string fileName)
        {
            command.Parameters.AddWithValue("@FileName", fileName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@MessageReference", order.MessageReference ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@DocumentNumber", order.DocumentNumber ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@DocumentDate", order.DocumentDate ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@DeliveryDate", order.DeliveryDate ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@BuyerId", order.BuyerId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@BuyerName", order.BuyerName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@SupplierId", order.SupplierId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@SupplierName", order.SupplierName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ItemNumber", order.ItemNumber ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Quantity", decimal.TryParse(order.Quantity, out decimal qty) ? qty : 0m);
            command.Parameters.AddWithValue("@UnitPrice", decimal.TryParse(order.UnitPrice, out decimal price) ? price : 0m);
            command.Parameters.AddWithValue("@LineAmount", decimal.TryParse(order.LineAmount, out decimal amount) ? amount : 0m);
        }

        private void AddCommonParameters(SqlCommand command, EDIOrder order)
        {
            command.Parameters.AddWithValue("@DocumentNumber", order.DocumentNumber ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@BuyerId", order.BuyerId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ItemNumber", order.ItemNumber ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Quantity", decimal.TryParse(order.Quantity, out decimal qty) ? qty : 0m);
            command.Parameters.AddWithValue("@LineAmount", decimal.TryParse(order.LineAmount, out decimal amount) ? amount : 0m);
        }
    }
}