using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;

namespace TripWise.Controllers.Api
{
    [Route("api/flights")]
    [ApiController]
    public class OrdersApiController : ControllerBase
    {
        private readonly TripWiseContext _context;

        public OrdersApiController(TripWiseContext context)
        {
            _context = context;
        }

        // ==================== АВИАБИЛЕТЫ ====================

        [HttpGet("my-orders")]
        public async Task<IActionResult> GetMyOrders()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "Не авторизован" });
                }

                var orders = await _context.FlightBookings
                    .Where(f => f.UserId == userId)
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => new
                    {
                        f.Id,
                        BookingNumber = f.BookingNumber,
                        f.FlightNumber,
                        f.Airline,
                        f.DepartureCity,
                        f.ArrivalCity,
                        f.DepartureDateTime,
                        f.ArrivalDateTime,
                        f.Price,
                        f.Currency,
                        Status = f.Status == (BookingStatus)3 ? "cancelled" :
                                 f.Status == (BookingStatus)2 ? "confirmed" : "pending",
                        f.Passengers,
                        f.TicketNumber,
                        f.CreatedAt,
                        f.BookingReference,
                        f.ContactName,
                        f.ContactEmail,
                        f.ContactPhone,
                        Type = "flight",
                        // ⬇️ ДОБАВЬТЕ ЭТИ ПОЛЯ ⬇️
                        f.SeatNumbers,
                        f.IsRoundTrip,
                        f.ReturnFlightNumber,
                        f.ReturnDepartureDateTime,
                        f.ReturnArrivalDateTime
                    })
                    .ToListAsync();

                return Ok(new { success = true, orders = orders });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("order/{orderId}")]
        public async Task<IActionResult> GetOrderDetails(string orderId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "Не авторизован" });
                }

                var order = await _context.FlightBookings
                    .FirstOrDefaultAsync(f => f.Id == orderId && f.UserId == userId);

                if (order == null)
                {
                    return NotFound(new { success = false, message = "Заказ не найден" });
                }

                List<object> passengers = new List<object>();
                if (!string.IsNullOrEmpty(order.PassengersJson))
                {
                    try
                    {
                        passengers = System.Text.Json.JsonSerializer.Deserialize<List<object>>(order.PassengersJson) ?? new List<object>();
                    }
                    catch { }
                }

                return Ok(new
                {
                    success = true,
                    order = new
                    {
                        order.Id,
                        BookingNumber = order.BookingNumber,
                        order.FlightNumber,
                        order.Airline,
                        order.DepartureCity,
                        order.ArrivalCity,
                        order.DepartureDateTime,
                        order.ArrivalDateTime,
                        order.Price,
                        order.Currency,
                        Status = order.Status == (BookingStatus)3 ? "cancelled" :
                                 order.Status == (BookingStatus)2 ? "confirmed" : "pending",
                        Passengers = passengers,
                        order.TicketNumber,
                        order.CreatedAt,
                        order.BookingReference,
                        order.ContactName,
                        order.ContactEmail,
                        order.ContactPhone,
                        order.Baggage,
                        order.HandLuggage,
                        order.Meal,
                        order.FlightClass,
                        order.IsRoundTrip,
                        order.ReturnFlightNumber,
                        order.ReturnDepartureDateTime,
                        order.ReturnArrivalDateTime,
                        Type = "flight"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("order/{orderId}/cancel")]
        public async Task<IActionResult> CancelOrder(string orderId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "Не авторизован" });
                }

                var order = await _context.FlightBookings
                    .FirstOrDefaultAsync(f => f.Id == orderId && f.UserId == userId);

                if (order == null)
                {
                    return NotFound(new { success = false, message = "Заказ не найден" });
                }

                // Разрешаем отмену для статусов Pending (1)
                if (order.Status != (BookingStatus)1)
                {
                    return BadRequest(new { success = false, message = "Этот заказ нельзя отменить" });
                }

                order.Status = (BookingStatus)3;
                order.CancelledAt = DateTime.UtcNow;
                order.CancellationReason = "Отменен пользователем";

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Заказ отменен" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ==================== Ж/Д БИЛЕТЫ ====================

        [HttpGet("my-train-orders")]
        public async Task<IActionResult> GetMyTrainOrders()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "Не авторизован" });
                }

                var orders = await _context.TrainOrders
                    .Where(t => t.UserId == userId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new
                    {
                        t.Id,
                        OrderNumber = t.OrderNumber,
                        TrainNumber = t.TrainNumber,
                        DepartureStation = t.DepartureStationName,
                        ArrivalStation = t.ArrivalStationName,
                        DepartureDateTime = t.DepartureDateTime,
                        ArrivalDateTime = t.ArrivalDateTime,
                        Price = t.TotalPrice,
                        t.Currency,
                        Status = t.Status == OrderStatus.Cancelled ? "cancelled" :
                                 t.Status == OrderStatus.Confirmed ? "confirmed" : "pending",
                        t.Passengers,
                        t.TicketNumber,
                        t.CreatedAt,
                        BookingReference = t.BookingReference,
                        t.ContactEmail,
                        t.ContactPhone,
                        t.CarType,
                        t.CarClass,
                        t.SeatNumbers,
                        t.CarNumber,
                        t.IsRoundTrip,
                        ReturnTrainNumber = t.ReturnTrainNumber,
                        ReturnDepartureDateTime = t.ReturnDepartureDateTime,
                        ReturnArrivalDateTime = t.ReturnArrivalDateTime,
                        Type = "train"
                    })
                    .ToListAsync();

                return Ok(new { success = true, orders = orders });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("train-order/{orderId}")]
        public async Task<IActionResult> GetTrainOrderDetails(string orderId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "Не авторизован" });
                }

                var order = await _context.TrainOrders
                    .FirstOrDefaultAsync(t => t.Id == orderId && t.UserId == userId);

                if (order == null)
                {
                    return NotFound(new { success = false, message = "Заказ не найден" });
                }

                // Пассажиры хранятся в отдельной таблице TrainPassengers
                var passengers = await _context.TrainPassengers
                    .Where(p => p.OrderId == orderId)
                    .Select(p => new
                    {
                        p.FirstName,
                        p.LastName,
                        p.MiddleName,
                        p.DateOfBirth,
                        p.DocumentNumber,
                        p.SeatNumber,
                        p.CarNumber
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    order = new
                    {
                        order.Id,
                        OrderNumber = order.OrderNumber,
                        TrainNumber = order.TrainNumber,
                        DepartureStation = order.DepartureStationName,
                        ArrivalStation = order.ArrivalStationName,
                        order.DepartureDateTime,
                        order.ArrivalDateTime,
                        order.TotalPrice,
                        order.Currency,
                        Status = order.Status == OrderStatus.Cancelled ? "cancelled" :
                                 order.Status == OrderStatus.Confirmed ? "confirmed" : "pending",
                        Passengers = passengers,
                        order.TicketNumber,
                        order.CreatedAt,
                        BookingReference = order.BookingReference,
                        order.ContactEmail,
                        order.ContactPhone,
                        order.CarType,
                        order.CarClass,
                        order.SeatNumbers,
                        order.CarNumber,
                        order.IsRoundTrip,
                        order.ReturnTrainNumber,
                        order.ReturnDepartureDateTime,
                        order.ReturnArrivalDateTime,
                        Type = "train"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("train-order/{orderId}/cancel")]
        public async Task<IActionResult> CancelTrainOrder(string orderId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "Не авторизован" });
                }

                var order = await _context.TrainOrders
                    .FirstOrDefaultAsync(t => t.Id == orderId && t.UserId == userId);

                if (order == null)
                {
                    return NotFound(new { success = false, message = "Заказ не найден" });
                }

                // Разрешаем отмену только для статуса Pending
                if (order.Status != OrderStatus.Pending)
                {
                    return BadRequest(new { success = false, message = "Этот заказ нельзя отменить" });
                }

                order.Status = OrderStatus.Cancelled;
                order.Notes = "Отменен пользователем";

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Заказ отменен" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ==================== ОТЕЛИ ====================

        [HttpGet("my-hotel-bookings")]
        public async Task<IActionResult> GetMyHotelBookings()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "Не авторизован" });
                }

                var bookings = await _context.HotelBookings
                    .Where(h => h.UserId == userId)
                    .OrderByDescending(h => h.CreatedAt)
                    .Select(h => new
                    {
                        h.Id,
                        BookingNumber = h.BookingNumber,
                        HotelName = h.HotelName,
                        HotelAddress = h.HotelAddress,
                        CheckInDate = h.CheckInDate,
                        CheckOutDate = h.CheckOutDate,
                        Nights = h.Nights,
                        TotalPrice = h.TotalPrice,
                        Price = h.TotalPrice,  // ← ДОБАВЬТЕ ЭТУ СТРОКУ
                        h.Currency,
                        Status = h.Status == BookingStatus.Cancelled ? "cancelled" :
                                 h.Status == BookingStatus.Confirmed ? "confirmed" : "pending",
                        Guests = h.Guests,
                        Rooms = h.Rooms,
                        h.CreatedAt,
                        h.ContactName,
                        h.ContactEmail,
                        h.ContactPhone,
                        Stars = h.Stars,
                        AccommodationType = h.AccommodationType,
                        Type = "hotel"
                    })
                    .ToListAsync();

                return Ok(new { success = true, orders = bookings });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("hotel-booking/{bookingId}")]
        public async Task<IActionResult> GetHotelBookingDetails(string bookingId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "Не авторизован" });
                }

                var booking = await _context.HotelBookings
                    .FirstOrDefaultAsync(h => h.Id == bookingId && h.UserId == userId);

                if (booking == null)
                {
                    return NotFound(new { success = false, message = "Бронирование не найдено" });
                }

                return Ok(new
                {
                    success = true,
                    order = new
                    {
                        booking.Id,
                        BookingNumber = booking.BookingNumber,
                        HotelName = booking.HotelName,
                        HotelAddress = booking.HotelAddress,
                        HotelPhone = booking.HotelPhone,
                        HotelWebsite = booking.HotelWebsite,
                        CheckInDate = booking.CheckInDate,
                        CheckOutDate = booking.CheckOutDate,
                        Nights = booking.Nights,
                        Guests = booking.Guests,
                        Rooms = booking.Rooms,
                        TotalPrice = booking.TotalPrice,
                        Price = booking.TotalPrice,  // ← ДОБАВЬТЕ ЭТУ СТРОКУ
                        PricePerNight = booking.PricePerNight,
                        booking.Currency,
                        Status = booking.Status == BookingStatus.Cancelled ? "cancelled" :
                                 booking.Status == BookingStatus.Confirmed ? "confirmed" : "pending",
                        booking.CreatedAt,
                        booking.ContactName,
                        booking.ContactEmail,
                        booking.ContactPhone,
                        Stars = booking.Stars,
                        AccommodationType = booking.AccommodationType,
                        SpecialRequests = booking.SpecialRequests,
                        Type = "hotel"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("hotel-booking/{bookingId}/cancel")]
        public async Task<IActionResult> CancelHotelBooking(string bookingId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "Не авторизован" });
                }

                var booking = await _context.HotelBookings
                    .FirstOrDefaultAsync(h => h.Id == bookingId && h.UserId == userId);

                if (booking == null)
                {
                    return NotFound(new { success = false, message = "Бронирование не найдено" });
                }

                // Разрешаем отмену только для статуса Pending
                if (booking.Status != BookingStatus.Pending)
                {
                    return BadRequest(new { success = false, message = "Это бронирование нельзя отменить" });
                }

                booking.Status = BookingStatus.Cancelled;
                booking.CancelledAt = DateTime.UtcNow;
                booking.CancellationReason = "Отменено пользователем";

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Бронирование отменено" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ==================== ВСЕ ЗАКАЗЫ ====================

        [HttpGet("all-orders")]
        public async Task<IActionResult> GetAllOrders()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "Не авторизован" });
                }

                var flightOrders = await _context.FlightBookings
    .Where(f => f.UserId == userId)
    .Select(f => new
    {
        f.Id,
        Number = f.BookingNumber,
        Title = $"{f.Airline} - {f.FlightNumber}",
        DepartureCity = f.DepartureCity,
        ArrivalCity = f.ArrivalCity,
        DepartureStation = (string)null,
        ArrivalStation = (string)null,
        HotelAddress = (string)null,
        CheckOutDate = (DateTime?)null,
        Nights = (int?)null,
        Guests = (int?)null,
        Rooms = (int?)null,
        Stars = (int?)null,
        AccommodationType = (string)null,
        Date = f.DepartureDateTime,
        Price = f.Price,
        f.Currency,
        Status = f.Status == (BookingStatus)3 ? "cancelled" :
                 f.Status == (BookingStatus)2 ? "confirmed" : "pending",
        f.CreatedAt,
        Type = "flight",
        Icon = "fa-plane",
        Color = "#0379D9",
        // Дополнительные поля для единообразия
        Airline = f.Airline,
        FlightNumber = f.FlightNumber,
        DepartureDateTime = f.DepartureDateTime,
        ArrivalDateTime = f.ArrivalDateTime,
        BookingNumber = f.BookingNumber,
        Passengers = f.Passengers,
        TicketNumber = f.TicketNumber,
        ContactName = f.ContactName,
        ContactEmail = f.ContactEmail,
        ContactPhone = f.ContactPhone,
        // Поля для обратного рейса
        IsRoundTrip = f.IsRoundTrip,
        ReturnFlightNumber = f.ReturnFlightNumber,
        ReturnDepartureDateTime = f.ReturnDepartureDateTime,
        ReturnArrivalDateTime = f.ReturnArrivalDateTime,
        ReturnDuration = f.ReturnDuration,
        ReturnTransfers = f.ReturnTransfers,
        // ⬇️ ДОБАВЬТЕ ПОЛЕ ДЛЯ МЕСТ ⬇️
        SeatNumbers = f.SeatNumbers
    })
    .ToListAsync();

                var trainOrders = await _context.TrainOrders
            .Where(t => t.UserId == userId)
            .Select(t => new
            {
                t.Id,
                Number = t.OrderNumber,
                Title = $"Поезд {t.TrainNumber}",
                DepartureCity = (string)null,
                ArrivalCity = (string)null,
                DepartureStation = t.DepartureStationName,
                ArrivalStation = t.ArrivalStationName,
                HotelAddress = (string)null,
                CheckOutDate = (DateTime?)null,
                Nights = (int?)null,
                Guests = (int?)null,
                Rooms = (int?)null,
                Stars = (int?)null,
                AccommodationType = (string)null,
                Date = t.DepartureDateTime,
                Price = t.TotalPrice,
                t.Currency,
                Status = t.Status == OrderStatus.Cancelled ? "cancelled" :
                         t.Status == OrderStatus.Confirmed ? "confirmed" : "pending",
                t.CreatedAt,
                Type = "train",
                Icon = "fa-train",
                Color = "#28a745",
                // Основные поля
                TrainNumber = t.TrainNumber,
                DepartureDateTime = t.DepartureDateTime,
                ArrivalDateTime = t.ArrivalDateTime,
                SeatNumbers = t.SeatNumbers,
                CarNumber = t.CarNumber,
                CarType = t.CarType,
                CarClass = t.CarClass,
                Passengers = t.Passengers,
                TotalPrice = t.TotalPrice,
                OrderNumber = t.OrderNumber,
                // ⬇️⬇️⬇️ ДОБАВЬТЕ ЭТИ ПОЛЯ ДЛЯ ОБРАТНОГО БИЛЕТА ⬇️⬇️⬇️
                IsRoundTrip = t.IsRoundTrip,
                ReturnTrainNumber = t.ReturnTrainNumber,
                ReturnDepartureDateTime = t.ReturnDepartureDateTime,
                ReturnArrivalDateTime = t.ReturnArrivalDateTime,
                ReturnDuration = t.ReturnDuration
            })
            .ToListAsync();

                var hotelBookings = await _context.HotelBookings
                    .Where(h => h.UserId == userId)
                    .Select(h => new
                    {
                        h.Id,
                        Number = h.BookingNumber,
                        Title = h.HotelName,
                        DepartureCity = (string)null,
                        ArrivalCity = (string)null,
                        DepartureStation = (string)null,
                        ArrivalStation = (string)null,
                        HotelAddress = h.HotelAddress,
                        CheckOutDate = (DateTime?)h.CheckOutDate,
                        Nights = h.Nights,
                        Guests = h.Guests,
                        Rooms = h.Rooms,
                        Stars = h.Stars,
                        AccommodationType = h.AccommodationType,
                        Date = h.CheckInDate,
                        Price = h.TotalPrice,
                        h.Currency,
                        Status = h.Status == BookingStatus.Cancelled ? "cancelled" :
                                 h.Status == BookingStatus.Confirmed ? "confirmed" : "pending",
                        h.CreatedAt,
                        Type = "hotel",
                        Icon = "fa-hotel",
                        Color = "#fd7e14",
                        // Дополнительные поля для отелей
                        HotelName = h.HotelName,
                        CheckInDate = h.CheckInDate,
                        TotalPrice = h.TotalPrice
                    })
                    .ToListAsync();

                var allOrders = flightOrders.Cast<object>()
                    .Concat(trainOrders)
                    .Concat(hotelBookings)
                    .OrderByDescending(o => ((dynamic)o).CreatedAt)
                    .ToList();

                return Ok(new { success = true, orders = allOrders });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ==================== ОПЛАТА ====================

        [HttpPost("pay/{orderId}")]
        public async Task<IActionResult> PayOrder(string orderId, [FromBody] PaymentRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "Не авторизован" });
                }

                // Имитация успешной оплаты
                await Task.Delay(1000);

                if (request.Type == "flight")
                {
                    var order = await _context.FlightBookings
                        .FirstOrDefaultAsync(f => f.Id == orderId && f.UserId == userId);
                    if (order == null)
                    {
                        return NotFound(new { success = false, message = "Заказ не найден" });
                    }
                    // Проверяем, что заказ еще не оплачен
                    if (order.Status == (BookingStatus)1) // Pending
                    {
                        order.Status = (BookingStatus)2; // Confirmed
                        order.PaymentStatus = PaymentStatus.Paid;
                        order.ConfirmedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        return BadRequest(new { success = false, message = "Этот заказ уже оплачен или недоступен для оплаты" });
                    }
                }
                else if (request.Type == "train")
                {
                    var order = await _context.TrainOrders
                        .FirstOrDefaultAsync(t => t.Id == orderId && t.UserId == userId);
                    if (order == null)
                    {
                        return NotFound(new { success = false, message = "Заказ не найден" });
                    }
                    if (order.Status == OrderStatus.Pending)
                    {
                        order.Status = OrderStatus.Confirmed;
                        order.PaymentStatus = PaymentStatus.Paid;
                        order.ConfirmedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        return BadRequest(new { success = false, message = "Этот заказ уже оплачен или недоступен для оплаты" });
                    }
                }
                else if (request.Type == "hotel")
                {
                    var booking = await _context.HotelBookings
                        .FirstOrDefaultAsync(h => h.Id == orderId && h.UserId == userId);
                    if (booking == null)
                    {
                        return NotFound(new { success = false, message = "Бронирование не найдено" });
                    }
                    if (booking.Status == BookingStatus.Pending)
                    {
                        booking.Status = BookingStatus.Confirmed;
                        booking.PaymentStatus = PaymentStatus.Paid;
                        booking.ConfirmedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        return BadRequest(new { success = false, message = "Это бронирование уже оплачено или недоступно для оплаты" });
                    }
                }

                return Ok(new { success = true, message = "Оплата прошла успешно" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    public class PaymentRequest
    {
        public string OrderId { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public string CardNumber { get; set; }
        public string CardHolder { get; set; }
    }
}