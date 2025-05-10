using APBD.Models;
using APBD.Services;
using Microsoft.AspNetCore.Mvc;

namespace APBD.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientsController : ControllerBase
    {
        private readonly IClientsService _clientsService;

        public ClientsController(IClientsService clientsService)
        {
            _clientsService = clientsService;
        }

        [HttpGet("{idClient}/trips")]
        public async Task<IActionResult> GetClientTrips(int idClient)
        {
            var clientTrips = await _clientsService.GetClientTripsAsync(idClient);
            if (clientTrips == null)
            {
                return NotFound($"Klient o ID {idClient} nie został znaleziony.");
            }
            return Ok(clientTrips);
        }

        [HttpPost]
        public async Task<IActionResult> CreateClient([FromBody] Client client)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var (createdClient, errorMessage) = await _clientsService.CreateClientAsync(client);
            if (createdClient == null)
            {
                return Conflict(errorMessage);
            }
            return CreatedAtAction(nameof(GetClientTrips), new { idClient = createdClient.IdClient }, createdClient);
        }

        [HttpPost("{idClient}/trips/{idTrip}")]
        public async Task<IActionResult> AssignClientToTrip(int idClient, int idTrip)
        {
            if (idClient <= 0 || idTrip <= 0)
            {
                return BadRequest("Nieprawidłowe ID klienta lub wycieczki.");
            }

            var result = await _clientsService.AssignClientToTripAsync(idClient, idTrip);

            if (!result.success)
            {
                if (result.clientNotFound) return NotFound($"Klient o ID {idClient} nie został znaleziony.");
                if (result.tripNotFound) return NotFound($"Wycieczka o ID {idTrip} nie została znaleziona.");
                if (result.tripDateInvalid) return BadRequest("Nie można zapisać się na wycieczkę, która już się odbyła.");
                if (result.alreadyRegistered) return Conflict("Klient jest już zarejestrowany na tę wycieczkę.");
                if (result.tripFull) return Conflict("Osiągnięto limit miejsc na wycieczkę.");
                return BadRequest(result.errorMessage ?? "Wystąpił nieoczekiwany błąd podczas przypisywania klienta do wycieczki.");
            }

            return Ok("Klient został pomyślnie zarejestrowany na wycieczkę.");
        }

        [HttpDelete("{idClient}")]
        public async Task<IActionResult> DeleteClient(int idClient)
        {
            if (!await _clientsService.ClientExistsAsync(idClient))
            {
                return NotFound($"Klient o ID {idClient} nie został znaleziony.");
            }

            if (await _clientsService.ClientHasTripsAsync(idClient))
            {
                return BadRequest("Nie można usunąć klienta, który ma przypisane wycieczki.");
            }

            var success = await _clientsService.DeleteClientAsync(idClient); 
            if (!success)
            {
                return NotFound($"Nie udało się usunąć klienta o ID {idClient} lub klient nie istnieje.");
            }

            return NoContent();
        }

        [HttpDelete("{idClient}/trips/{idTrip}")]
        public async Task<IActionResult> DeleteClientRegistration(int idClient, int idTrip)
        {
            var success = await _clientsService.DeleteClientRegistrationAsync(idClient, idTrip);
            if (!success)
            {
                return NotFound("Nie znaleziono rejestracji klienta na tę wycieczkę lub wystąpił błąd podczas usuwania.");
            }
            return NoContent();
        }
    }
}
