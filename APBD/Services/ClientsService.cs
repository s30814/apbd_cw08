using APBD.Models;
using Microsoft.Data.SqlClient;

namespace APBD.Services
{
    public class ClientsService : IClientsService
    {
        private readonly string _connectionString = "Data Source=(localdb)\\MSSQLLocalDB;Integrated Security=True;";

        //
        public async Task<bool> ClientExistsAsync(int idClient)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var commandText = "SELECT COUNT(1) FROM Client WHERE IdClient = @IdClient";
                using (var command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@IdClient", idClient);
                    return (int)await command.ExecuteScalarAsync() > 0;
                }
            }
        }

        private async Task<(bool exists, DateTime dateFrom, int maxPeople)> GetTripDetailsAsync(int idTrip)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var commandText = "SELECT DateFrom, MaxPeople FROM Trip WHERE IdTrip = @IdTrip";
                using (var command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@IdTrip", idTrip);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return (true, reader.GetDateTime(0), reader.GetInt32(1));
                        }
                    }
                }
            }
            return (false, DateTime.MinValue, 0);
        }

        public async Task<bool> IsClientRegisteredForTripAsync(int idClient, int idTrip)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var commandText = "SELECT COUNT(1) FROM Client_Trip WHERE IdClient = @IdClient AND IdTrip = @IdTrip";
                using (var command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@IdClient", idClient);
                    command.Parameters.AddWithValue("@IdTrip", idTrip);
                    return (int)await command.ExecuteScalarAsync() > 0;
                }
            }
        }

        public async Task<int> GetRegisteredClientsCountForTripAsync(int idTrip)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var commandText = "SELECT COUNT(1) FROM Client_Trip WHERE IdTrip = @IdTrip";
                using (var command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@IdTrip", idTrip);
                    return (int)await command.ExecuteScalarAsync();
                }
            }
        }


        public async Task<IEnumerable<ClientTripDetails>> GetClientTripsAsync(int idClient)
        {
            if (!await ClientExistsAsync(idClient))
            {
                return null;
            }

            var clientTrips = new List<ClientTripDetails>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var commandText = @"
                    SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                           ctrip.RegisteredAt, ctrip.PaymentDate,
                           c.IdCountry, c.Name AS CountryName
                    FROM Client_Trip ctrip
                    JOIN Trip t ON ctrip.IdTrip = t.IdTrip
                    LEFT JOIN Country_Trip cot ON t.IdTrip = cot.IdTrip
                    LEFT JOIN Country c ON cot.IdCountry = c.IdCountry
                    WHERE ctrip.IdClient = @IdClient
                    ORDER BY t.DateFrom DESC;";

                using (var command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@IdClient", idClient);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var tripDictionary = new Dictionary<int, ClientTripDetails>();
                        while (await reader.ReadAsync())
                        {
                            var tripId = reader.GetInt32(reader.GetOrdinal("IdTrip"));
                            if (!tripDictionary.TryGetValue(tripId, out var tripDetail))
                            {
                                tripDetail = new ClientTripDetails
                                {
                                    IdTrip = tripId,
                                    Name = reader["Name"].ToString(),
                                    Description = reader["Description"].ToString(),
                                    DateFrom = reader.GetDateTime(reader.GetOrdinal("DateFrom")),
                                    DateTo = reader.GetDateTime(reader.GetOrdinal("DateTo")),
                                    MaxPeople = reader.GetInt32(reader.GetOrdinal("MaxPeople")),
                                    RegisteredAt = reader.GetInt32(reader.GetOrdinal("RegisteredAt")),
                                    PaymentDate = reader.IsDBNull(reader.GetOrdinal("PaymentDate")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("PaymentDate")),
                                    Countries = new List<Country>()
                                };
                                tripDictionary.Add(tripId, tripDetail);
                                clientTrips.Add(tripDetail);
                            }
                            if (!reader.IsDBNull(reader.GetOrdinal("IdCountry")))
                            {
                                tripDetail.Countries.Add(new Country
                                {
                                    IdCountry = reader.GetInt32(reader.GetOrdinal("IdCountry")),
                                    Name = reader["CountryName"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            return clientTrips;
        }

        public async Task<(Client? client, string? errorMessage)> CreateClientAsync(Client client)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var checkPeselCmd = new SqlCommand("SELECT COUNT(1) FROM Client WHERE Pesel = @Pesel AND IdClient != @IdClientToExclude", connection); 
                checkPeselCmd.Parameters.AddWithValue("@Pesel", client.Pesel);
                checkPeselCmd.Parameters.AddWithValue("@IdClientToExclude", -1); 
                if ((int)await checkPeselCmd.ExecuteScalarAsync() > 0)
                {
                    return (null, "Klient z tym numerem PESEL już istnieje.");
                }

                var commandText = @"
                    INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
                    OUTPUT INSERTED.IdClient
                    VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel);";
                using (var command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@FirstName", client.FirstName);
                    command.Parameters.AddWithValue("@LastName", client.LastName);
                    command.Parameters.AddWithValue("@Email", client.Email);
                    command.Parameters.AddWithValue("@Telephone", client.Telephone);
                    command.Parameters.AddWithValue("@Pesel", client.Pesel);

                    client.IdClient = (int)await command.ExecuteScalarAsync();
                    return (client, null);
                }
            }
        }

        public async Task<(bool success, string? errorMessage, bool clientNotFound, bool tripNotFound, bool tripDateInvalid, bool alreadyRegistered, bool tripFull)> AssignClientToTripAsync(int idClient, int idTrip)
        {
            if (!await ClientExistsAsync(idClient))
                return (false, "Klient nie istnieje.", true, false, false, false, false);

            var tripDetails = await GetTripDetailsAsync(idTrip);
            if (!tripDetails.exists)
                return (false, "Wycieczka nie istnieje.", false, true, false, false, false);

            if (tripDetails.dateFrom < DateTime.Now)
                return (false, "Nie można zapisać się na wycieczkę, która już się odbyła.", false, false, true, false, false);

            if (await IsClientRegisteredForTripAsync(idClient, idTrip))
                return (false, "Klient jest już zarejestrowany na tę wycieczkę.", false, false, false, true, false);

            if (await GetRegisteredClientsCountForTripAsync(idTrip) >= tripDetails.maxPeople)
                return (false, "Osiągnięto limit miejsc na wycieczkę.", false, false, false, false, true);

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var commandText = @"
                    INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt)
                    VALUES (@IdClient, @IdTrip, @RegisteredAt);";
                using (var command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@IdClient", idClient);
                    command.Parameters.AddWithValue("@IdTrip", idTrip);
                    command.Parameters.AddWithValue("@RegisteredAt", int.Parse(DateTime.UtcNow.Date.ToString("yyyyMMdd")));
                    await command.ExecuteNonQueryAsync();
                    return (true, null, false, false, false, false, false);
                }
            }
        }

        public async Task<bool> ClientHasTripsAsync(int idClient)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var commandText = "SELECT COUNT(1) FROM Client_Trip WHERE IdClient = @IdClient";
                using (var command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@IdClient", idClient);
                    return (int)await command.ExecuteScalarAsync() > 0;
                }
            }
        }

        public async Task<bool> DeleteClientAsync(int idClient)
        {
            if (await ClientHasTripsAsync(idClient))
            {
                return false;
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var commandText = "DELETE FROM Client WHERE IdClient = @IdClient";
                using (var command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@IdClient", idClient);
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<bool> DeleteClientRegistrationAsync(int idClient, int idTrip)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                if (!await IsClientRegisteredForTripAsync(idClient, idTrip))
                {
                    return false;
                }

                var commandText = @"
                    DELETE FROM Client_Trip
                    WHERE IdClient = @IdClient AND IdTrip = @IdTrip;";
                using (var command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@IdClient", idClient);
                    command.Parameters.AddWithValue("@IdTrip", idTrip);
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }
    }
}
