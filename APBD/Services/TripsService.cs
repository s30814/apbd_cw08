using APBD.Models;
using Microsoft.Data.SqlClient;

namespace APBD.Services
{
    public class TripsService : ITripsService
    {
        private readonly string _connectionString = "Data Source=(localdb)\\MSSQLLocalDB;Integrated Security=True;";

        public async Task<IEnumerable<Trip>> GetTripsAsync()
        {
            var trips = new List<Trip>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var commandText = @"
                    SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                           c.IdCountry, c.Name AS CountryName
                    FROM Trip t
                    LEFT JOIN Country_Trip ct ON t.IdTrip = ct.IdTrip
                    LEFT JOIN Country c ON ct.IdCountry = c.IdCountry
                    ORDER BY t.DateFrom DESC;";

                using (var command = new SqlCommand(commandText, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var tripDictionary = new Dictionary<int, Trip>();
                        while (await reader.ReadAsync())
                        {
                            var tripId = reader.GetInt32(reader.GetOrdinal("IdTrip"));
                            if (!tripDictionary.TryGetValue(tripId, out var trip))
                            {
                                trip = new Trip
                                {
                                    IdTrip = tripId,
                                    Name = reader["Name"].ToString(),
                                    Description = reader["Description"].ToString(),
                                    DateFrom = (DateTime)reader["DateFrom"],
                                    DateTo = (DateTime)reader["DateTo"],
                                    MaxPeople = (int)reader["MaxPeople"],
                                    Countries = new List<Country>()
                                };
                                tripDictionary.Add(tripId, trip);
                                trips.Add(trip);
                            }
                            if (!reader.IsDBNull(reader.GetOrdinal("IdCountry")))
                            {
                                trip.Countries.Add(new Country
                                {
                                    IdCountry = reader.GetInt32(reader.GetOrdinal("IdCountry")),
                                    Name = reader["CountryName"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            return trips;
        }
    }
}
