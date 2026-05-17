// Глобальные переменные
let currentDropdown = null;
let currentInput = null;
let timeoutId;
let isUserAuthenticated = false;
let userId = null;

// ==================== ФУНКЦИИ АВТОРИЗАЦИИ ====================
async function checkAuthStatus() {
    try {
        const response = await fetch('/Account/GetAuthStatus', {
            credentials: 'include',
            headers: { 'Accept': 'application/json' }
        });

        if (response.ok) {
            const data = await response.json();
            const wasAuthenticated = isUserAuthenticated;
            isUserAuthenticated = data.isAuthenticated;
            userId = data.userId;
            console.log('Статус авторизации:', isUserAuthenticated, 'User ID:', userId);

            if (isUserAuthenticated) {
                await loadFavorites();
            }

            // Обновляем кнопки покупки при изменении статуса
            if (wasAuthenticated !== isUserAuthenticated) {
                updateAllBuyButtons();
            }
        }
    } catch (error) {
        console.error('Ошибка проверки авторизации:', error);
        isUserAuthenticated = false;
        userId = null;
    }
}

// ==================== ФУНКЦИИ ИЗБРАННОГО ====================
async function loadFavorites() {
    if (!isUserAuthenticated) {
        console.log('Пользователь не авторизован, избранное не загружается');
        return;
    }

    try {
        console.log('Загрузка избранного...');
        const response = await fetch('/api/favorites/list', {
            credentials: 'include',
            headers: { 'Accept': 'application/json' }
        });

        if (response.ok) {
            const data = await response.json();
            if (data.success && data.favorites) {
                window.favoriteFlights = new Set(data.favorites);
                console.log('Загружено избранное:', Array.from(window.favoriteFlights));
                setTimeout(() => {
                    updateAllFavoriteButtons();
                }, 200);
            } else {
                window.favoriteFlights = new Set();
            }
        } else {
            console.error('Ошибка загрузки избранного:', response.status);
            window.favoriteFlights = new Set();
        }
    } catch (error) {
        console.error('Ошибка загрузки избранного:', error);
        window.favoriteFlights = new Set();
    }
}

async function toggleFavorite(flightData) {
    if (!isUserAuthenticated) {
        showAuthRequiredModal();
        return;
    }

    const button = document.querySelector(`[data-flight-id="${CSS.escape(flightData.flightId)}"]`);
    if (button) {
        button.style.pointerEvents = 'none';
        button.style.opacity = '0.6';
    }

    try {
        const flightId = flightData.flightId;
        const isCurrentlyFavorite = window.favoriteFlights?.has(flightId) || false;

        let url, method;
        if (isCurrentlyFavorite) {
            url = '/api/favorites/remove';
            method = 'POST';
        } else {
            url = '/api/favorites/add';
            method = 'POST';
        }

        console.log(`Отправка запроса ${method} ${url}`, flightData);

        const response = await fetch(url, {
            method: method,
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            credentials: 'include',
            body: JSON.stringify(isCurrentlyFavorite ?
                { flightId: flightId } :
                {
                    flightId: flightData.flightId,
                    airline: flightData.airline || 'Авиакомпания',
                    airlineCode: flightData.airlineCode || '',
                    flightNumber: flightData.flightNumber || '',
                    departureCity: flightData.departureCity || '',
                    arrivalCity: flightData.arrivalCity || '',
                    departureAirport: flightData.departureAirport || '',
                    arrivalAirport: flightData.arrivalAirport || '',
                    departureTime: flightData.departureTime || new Date().toISOString(),
                    arrivalTime: flightData.arrivalTime || new Date().toISOString(),
                    price: flightData.price || 0,
                    currency: flightData.currency || 'RUB',
                    transfers: flightData.transfers || 0,
                    duration: flightData.duration || 0,
                    aircraft: flightData.aircraft || '',
                    isReturn: flightData.isReturn || false,
                    bookingUrl: flightData.bookingUrl || ''
                }
            )
        });

        if (!response.ok) {
            throw new Error(`HTTP ошибка: ${response.status}`);
        }

        const result = await response.json();
        console.log('Результат операции:', result);

        if (result.success) {
            if (isCurrentlyFavorite) {
                window.favoriteFlights.delete(flightId);
                showNotification('Рейс удален из избранного', 'success');
            } else {
                window.favoriteFlights.add(flightId);
                showNotification('Рейс добавлен в избранное!', 'success');
            }
            updateFavoriteButton(flightId, !isCurrentlyFavorite);
        } else {
            showNotification(result.message || 'Ошибка при сохранении', 'danger');
        }
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Ошибка при сохранении рейса: ' + error.message, 'danger');
    } finally {
        if (button) {
            button.style.pointerEvents = '';
            button.style.opacity = '';
        }
    }
}

async function handleFavoriteClick(button) {
    if (button.disabled) return;
    button.disabled = true;

    const flightId = button.getAttribute('data-flight-id');
    const flightDataStr = button.getAttribute('data-flight-data');

    if (!flightDataStr) {
        console.error('Данные рейса не найдены');
        button.disabled = false;
        return;
    }

    try {
        const flightData = JSON.parse(flightDataStr.replace(/&apos;/g, "'"));
        await toggleFavorite(flightData);
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Ошибка при сохранении рейса', 'danger');
    } finally {
        setTimeout(() => { button.disabled = false; }, 500);
    }
}

function updateFavoriteButton(flightId, isFavorite) {
    const buttons = document.querySelectorAll(`[data-flight-id="${CSS.escape(flightId)}"]`);
    buttons.forEach(button => {
        const icon = button.querySelector('i');
        if (icon) {
            if (isFavorite) {
                icon.className = 'fas fa-heart text-danger fa-lg';
                button.title = 'Удалить из избранного';
                button.classList.add('favorited');
            } else {
                icon.className = 'far fa-heart fa-lg text-muted';
                button.title = 'Добавить в избранное';
                button.classList.remove('favorited');
            }
        }
    });
}

function updateAllFavoriteButtons() {
    if (!window.favoriteFlights) {
        console.log('favoriteFlights не инициализирован');
        return;
    }

    const buttons = document.querySelectorAll('.favorite-btn');
    console.log('Обновление кнопок избранного:', buttons.length);

    buttons.forEach(button => {
        const flightId = button.getAttribute('data-flight-id');
        if (flightId) {
            const isFavorite = window.favoriteFlights.has(flightId);
            const icon = button.querySelector('i');
            if (icon) {
                if (isFavorite) {
                    icon.className = 'fas fa-heart text-danger fa-lg';
                    button.title = 'Удалить из избранного';
                    button.classList.add('favorited');
                } else {
                    icon.className = 'far fa-heart fa-lg text-muted';
                    button.title = 'Добавить в избранное';
                    button.classList.remove('favorited');
                }
            }
        }
    });
}

function resetFavoriteButton(flightId) {
    const buttons = document.querySelectorAll(`[data-flight-id="${CSS.escape(flightId)}"]`);
    buttons.forEach(button => {
        const icon = button.querySelector('i');
        if (icon) {
            icon.className = 'far fa-heart fa-lg text-muted';
            button.title = 'Добавить в избранное';
            button.classList.remove('favorited');
        }
    });
}

// ==================== ОБНОВЛЕНИЕ КНОПОК ПОКУПКИ ====================
function updateAllBuyButtons() {
    const isAuth = isUserAuthenticated;

    const flightCards = document.querySelectorAll('.flight-card');

    flightCards.forEach(card => {
        const priceDiv = card.querySelector('.text-end');
        if (!priceDiv) return;

        const price = parseFloat(priceDiv.querySelector('h3')?.textContent?.replace(/\s/g, '') || '0');
        const airline = card.querySelector('h6')?.textContent || '';
        const isReturn = card.classList.contains('return-flight');
        const typeColor = isReturn ? 'success' : 'primary';

        const oldBtn = priceDiv.querySelector('.btn');
        if (oldBtn) {
            const favoriteBtn = card.querySelector('.favorite-btn');
            let flightId = null;
            if (favoriteBtn) {
                flightId = favoriteBtn.getAttribute('data-flight-id');
            }

            if (isAuth) {
                const newBtn = document.createElement('button');
                newBtn.className = `btn btn-${typeColor} btn-lg px-4 mt-2 fw-bold`;
                newBtn.innerHTML = '<i class="fas fa-shopping-cart me-2"></i>Купить';
                newBtn.onclick = () => {
                    if (flightId) {
                        selectRealFlight(flightId, price, airline, isReturn);
                    }
                };
                oldBtn.replaceWith(newBtn);
            } else {
                const newBtn = document.createElement('button');
                newBtn.className = `btn btn-outline-${typeColor} btn-lg px-4 mt-2 fw-bold`;
                newBtn.innerHTML = '<i class="fas fa-lock me-2"></i>Войдите, чтобы купить';
                newBtn.onclick = () => showAuthRequiredModal();
                oldBtn.replaceWith(newBtn);
            }
        }
    });
}

// ==================== АВТОЗАПОЛНЕНИЕ ГОРОДОВ ====================
async function searchCitiesFromTravelPayouts(query, dropdown) {
    if (query.length < 2) {
        dropdown.style.display = 'none';
        return;
    }

    try {
        const endpoint = `https://autocomplete.travelpayouts.com/places2?term=${encodeURIComponent(query)}&locale=ru&types[]=airport&types[]=city`;
        console.log('Searching cities with query:', query);

        const response = await fetch(endpoint, {
            method: 'GET',
            headers: {
                'Accept': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
        });

        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);

        const data = await response.json();
        console.log('Received cities data:', data);

        let cities = [];
        if (Array.isArray(data)) {
            cities = data.map(item => {
                if (item.code && item.name) {
                    return {
                        name: item.name,
                        country: item.country_name || item.country_code || '',
                        airport: item.type === 'airport' ? item.name : '',
                        code: item.code,
                        type: item.type || 'city'
                    };
                }
                return null;
            }).filter(item => item !== null && item.name && item.code);
        }

        console.log('Processed cities:', cities);
        showAutocompleteResults(cities, dropdown, query);
    } catch (error) {
        console.error('Ошибка при поиске городов:', error);
        const mockCities = getMockCities(query);
        showAutocompleteResults(mockCities, dropdown, query);
    }
}

function getMockCities(query) {
    const allCities = [
        { code: "MOW", name: "Москва", country: "Россия", type: "city", airport: "" },
        { code: "LED", name: "Санкт-Петербург", country: "Россия", type: "city", airport: "" },
        { code: "AER", name: "Сочи", country: "Россия", type: "city", airport: "" },
        { code: "KZN", name: "Казань", country: "Россия", type: "city", airport: "" },
        { code: "SVX", name: "Екатеринбург", country: "Россия", type: "city", airport: "" },
        { code: "OVB", name: "Новосибирск", country: "Россия", type: "city", airport: "" },
        { code: "KRR", name: "Краснодар", country: "Россия", type: "city", airport: "" },
        { code: "SIP", name: "Симферополь", country: "Россия", type: "city", airport: "" },
        { code: "MRV", name: "Минеральные Воды", country: "Россия", type: "city", airport: "" },
        { code: "KGD", name: "Калининград", country: "Россия", type: "city", airport: "" },
        { code: "TJM", name: "Тюмень", country: "Россия", type: "city", airport: "" },
        { code: "SVO", name: "Шереметьево", country: "Россия", type: "airport", airport: "Шереметьево" },
        { code: "DME", name: "Домодедово", country: "Россия", type: "airport", airport: "Домодедово" },
        { code: "VKO", name: "Внуково", country: "Россия", type: "airport", airport: "Внуково" }
    ];

    return allCities.filter(city =>
        city.name.toLowerCase().includes(query.toLowerCase()) ||
        city.code.toLowerCase().includes(query.toLowerCase())
    );
}

function showAutocompleteResults(cities, dropdown, query) {
    dropdown.innerHTML = '';
    if (!cities || cities.length === 0) {
        const noResults = document.createElement('div');
        noResults.className = 'autocomplete-item';
        noResults.textContent = 'Ничего не найдено';
        dropdown.appendChild(noResults);
        dropdown.style.display = 'block';
        return;
    }

    const limitedCities = cities.slice(0, 8);
    limitedCities.forEach(city => {
        const item = document.createElement('div');
        item.className = 'autocomplete-item';

        let displayText = '';
        if (city.type === 'airport') {
            displayText = `
                <div class="city-name">${city.name}
                    <span class="city-country">${city.country}</span>
                </div>
                <div class="city-airport">Аэропорт (${city.code})</div>
            `;
        } else {
            displayText = `
                <div class="city-name">${city.name}
                    <span class="city-country">${city.country}</span>
                </div>
                <div class="city-airport">Город (${city.code})</div>
            `;
        }

        item.innerHTML = displayText;
        item.addEventListener('click', () => {
            let displayValue = `${city.name} (${city.code})`;
            currentInput.value = displayValue;
            dropdown.style.display = 'none';
        });

        dropdown.appendChild(item);
    });

    dropdown.style.display = 'block';
}

// ==================== ПОИСК РЕЙСОВ ====================
async function searchFlights(searchData) {
    try {
        console.log('Отправка запроса к API:', searchData);
        const response = await fetch('/api/flights/search', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(searchData)
        });

        console.log('Статус ответа:', response.status);
        if (!response.ok) {
            let errorText = `HTTP error! status: ${response.status}`;
            try {
                const errorData = await response.json();
                errorText = errorData.error || errorData.message || errorText;
            } catch (e) {
                console.log('Не удалось распарсить ошибку:', e);
            }
            throw new Error(errorText);
        }

        const result = await response.json();
        console.log('Ответ от API:', result);

        if (result.success && result.flights && result.flights.length > 0) {
            // Фильтруем только прямые рейсы
            let directFlights = result.flights.filter(flight => flight.transfers === 0);
            console.log(`Всего рейсов: ${result.flights.length}, прямых: ${directFlights.length}`);

            // Обрабатываем рейсы для правильного разделения на туда/обратно
            directFlights = processApiFlights(directFlights, searchData);

            result.flights = directFlights;
            result.message = `Найдено ${directFlights.length} прямых рейсов`;

            if (directFlights.length === 0) {
                result.message = 'Прямые рейсы не найдены. Попробуйте другие даты.';
            }
        }

        return result;
    } catch (error) {
        console.error('Ошибка при поиске рейсов:', error);
        throw error;
    }
}
function showFlightResults(flights, searchData) {
    console.log('=== ПОКАЗ РЕЗУЛЬТАТОВ ===');
    console.log('Получено рейсов:', flights?.length || 0);
    console.log('Пользователь авторизован:', isUserAuthenticated);

    const searchId = Date.now() + Math.random().toString(36);
    window.currentSearchId = searchId;

    const oldResults = document.getElementById('flightResultsContainer');
    if (oldResults) oldResults.innerHTML = '';

    if (!flights || flights.length === 0) {
        document.getElementById('flightResultsContainer').innerHTML = `
            <div class="alert alert-info mt-4">
                <h5 class="alert-heading">Рейсы не найдены</h5>
                <p>Попробуйте изменить параметры поиска или даты</p>
            </div>
        `;
        return;
    }

    // Разделяем рейсы на туда и обратно
    const oneWayFlights = flights.filter(flight => !flight.isReturn);
    const returnFlights = flights.filter(flight => flight.isReturn);
    const hasReturnDate = searchData.returnDate && searchData.returnDate !== null && searchData.returnDate !== '';

    let html = `
        <div class="card shadow-lg border-0 mb-4 mt-4">
            <div class="card-header bg-primary text-white py-3">
                <div class="d-flex justify-content-between align-items-center">
                    <h4 class="mb-0">
                        <i class="fas fa-plane me-2"></i>
                        Найдено рейсов: <span class="badge bg-light text-primary">${flights.length}</span>
                    </h4>
                    <div class="text-end">
                        <small class="d-block">${hasReturnDate ? 'Туда и обратно' : 'В одну сторону'}</small>
                        <small class="d-block">${searchData.passengers} пассажир${searchData.passengers > 1 ? 'а' : ''}</small>
                    </div>
                </div>
            </div>
            <div class="card-body p-0">
    `;

    // Рейсы ТУДА
    if (oneWayFlights.length > 0) {
        html += `
            <div class="flight-section-header section-tuda p-3" style="background: #f0f7ff; border-bottom: 1px solid #d4e4f5;">
                <div class="d-flex align-items-center">
                    <i class="fas fa-plane-departure fa-2x text-primary me-3"></i>
                    <div>
                        <h5 class="mb-1 fw-bold">Рейсы туда</h5>
                        <p class="mb-0 text-muted">
                            ${searchData.departureCity} → ${searchData.arrivalCity}
                            <span class="ms-2 badge bg-primary">${formatDateForDisplay(searchData.departureDate)}</span>
                        </p>
                    </div>
                </div>
            </div>
        `;
        oneWayFlights.forEach((flight, index) => {
            html += generateFlightCard(flight, index, false);
        });
    }

    // Рейсы ОБРАТНО (если есть дата возврата)
    if (hasReturnDate && returnFlights.length > 0) {
        html += `
            <div class="flight-section-header section-obratno p-3" style="background: #e8f5e9; border-bottom: 1px solid #c8e6c9;">
                <div class="d-flex align-items-center">
                    <i class="fas fa-plane-arrival fa-2x text-success me-3"></i>
                    <div>
                        <h5 class="mb-1 fw-bold">Рейсы обратно</h5>
                        <p class="mb-0 text-muted">
                            ${searchData.arrivalCity} → ${searchData.departureCity}
                            <span class="ms-2 badge bg-success">${formatDateForDisplay(searchData.returnDate)}</span>
                        </p>
                    </div>
                </div>
            </div>
        `;
        returnFlights.forEach((flight, index) => {
            html += generateFlightCard(flight, index, true);
        });
    } else if (hasReturnDate && returnFlights.length === 0) {
        html += `
            <div class="alert alert-warning m-3">
                <i class="fas fa-exclamation-triangle me-2"></i>
                Обратные рейсы не найдены. Показаны только рейсы туда.
            </div>
        `;
    }

    html += `
            </div>
            <div class="card-footer bg-light py-3">
                <div class="row">
                    <div class="col-md-12 text-end">
                        <small class="text-muted">
                            <i class="fas fa-sync-alt me-1"></i>
                            Данные обновлены: ${new Date().toLocaleTimeString('ru-RU')}
                        </small>
                    </div>
                </div>
            </div>
        </div>
    `;

    document.getElementById('flightResultsContainer').innerHTML = html;

    // Инициализируем кнопки избранного
    const favoriteButtons = document.querySelectorAll('.favorite-btn');
    favoriteButtons.forEach(button => {
        button.removeEventListener('click', handleFavoriteClick);
        button.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            handleFavoriteClick(this);
        });
    });

    // Обновляем состояние кнопок избранного
    if (isUserAuthenticated && window.favoriteFlights) {
        setTimeout(() => {
            if (window.currentSearchId === searchId) {
                updateAllFavoriteButtons();
                console.log('Состояние кнопок избранного обновлено');
            }
        }, 200);
    }

    // Если пользователь не авторизован, заменяем кнопки покупки
    if (!isUserAuthenticated) {
        const buyButtons = document.querySelectorAll('.btn-primary, .btn-success');
        buyButtons.forEach(btn => {
            if (btn.textContent.includes('Купить')) {
                const isSuccess = btn.classList.contains('btn-success');
                const newBtn = document.createElement('button');
                newBtn.className = isSuccess ? 'btn btn-outline-success btn-lg px-4 mt-2 fw-bold' : 'btn btn-outline-primary btn-lg px-4 mt-2 fw-bold';
                newBtn.innerHTML = '<i class="fas fa-lock me-2"></i>Войдите, чтобы купить';
                newBtn.onclick = () => showAuthRequiredModal();
                btn.replaceWith(newBtn);
            }
        });
    }

    setTimeout(() => {
        const resultsElement = document.getElementById('flightResultsContainer');
        if (resultsElement) {
            resultsElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }, 200);

    console.log('Результаты отображены');
}

function generateFlightCard(flight, index, isReturnFlight) {
    if (!flight) return '';

    const departureTime = formatTime(flight.departureTime);
    const arrivalTime = formatTime(flight.arrivalTime);
    const durationHours = Math.floor(flight.duration / 60);
    const durationMinutes = flight.duration % 60;
    const durationText = `${durationHours}ч ${durationMinutes}м`;

    const flightId = buildStableFlightId(flight, isReturnFlight);
    const typeColor = isReturnFlight ? 'success' : 'primary';
    const typeIcon = isReturnFlight ? 'fa-plane-arrival' : 'fa-plane-departure';
    const typeClass = isReturnFlight ? 'return-flight' : 'oneway-flight';  // ВАЖНО: правильный класс!
    const priceFormatted = flight.price ? flight.price.toLocaleString('ru-RU') : '0';
    const currency = flight.currency || 'RUB';

    const flightData = {
        flightId: flightId,
        airline: flight.airline || 'Авиакомпания',
        airlineCode: flight.airlineCode || '',
        flightNumber: flight.flightNumber || 'Рейс',
        departureCity: flight.departureCity || '',
        arrivalCity: flight.arrivalCity || '',
        departureAirport: flight.departureAirport || '',
        arrivalAirport: flight.arrivalAirport || '',
        departureTime: flight.departureTime,
        arrivalTime: flight.arrivalTime,
        price: flight.price || 0,
        currency: flight.currency || 'RUB',
        transfers: flight.transfers || 0,
        duration: flight.duration || 0,
        aircraft: flight.aircraft || '',
        isReturn: isReturnFlight,
        bookingUrl: flight.bookingUrl || '#'
    };

    const isAuth = isUserAuthenticated;

    let buyButtonHtml = '';
    if (isAuth) {
        buyButtonHtml = `
            <button class="btn btn-${typeColor} btn-lg px-4 mt-2 fw-bold"
                    onclick="selectRealFlight('${flightId}', ${flight.price || 0}, '${flight.airline || ''}', ${isReturnFlight})">
                <i class="fas fa-shopping-cart me-2"></i>Купить
            </button>
        `;
    } else {
        buyButtonHtml = `
            <button class="btn btn-outline-${typeColor} btn-lg px-4 mt-2 fw-bold"
                    onclick="showAuthRequiredModal()"
                    title="Войдите в аккаунт для покупки билетов">
                <i class="fas fa-lock me-2"></i>Войдите, чтобы купить
            </button>
        `;
    }

    return `
        <div class="flight-card ${typeClass} border-bottom p-4" data-flight-type="${isReturnFlight ? 'return' : 'oneway'}">
            <div class="row align-items-center">
                <div class="col-md-2">
                    <div class="d-flex align-items-center">
                        <i class="fas ${typeIcon} text-${typeColor} fa-lg me-3"></i>
                        <div>
                            <h6 class="mb-1 fw-bold">${flight.airline || 'Авиакомпания'}</h6>
                            <small class="text-muted">${flight.flightNumber || 'Рейс'}</small>
                        </div>
                    </div>
                </div>

                <div class="col-md-5">
                    <div class="row align-items-center">
                        <div class="col-4 text-end">
                            <div class="fw-bold fs-5 time-display text-${typeColor}">${departureTime}</div>
                            <small class="text-muted d-block">${flight.departureAirport || ''}</small>
                            <small class="text-muted">${flight.departureCity || ''}</small>
                        </div>

                        <div class="col-4 text-center">
                            <div class="flight-duration position-relative">
                                <div class="position-relative">
                                    <i class="fas fa-plane text-${typeColor} fa-lg"></i>
                                </div>
                                <div class="small text-muted mt-2">${durationText}</div>
                                ${flight.transfers > 0 ?
            `<div class="transfer-info small mt-1">${flight.transfers} пересад${flight.transfers === 1 ? 'ка' : 'ки'}</div>` :
            '<div class="text-success small mt-1">Прямой рейс</div>'
        }
                            </div>
                        </div>

                        <div class="col-4">
                            <div class="fw-bold fs-5 time-display text-${typeColor}">${arrivalTime}</div>
                            <small class="text-muted d-block">${flight.arrivalAirport || ''}</small>
                            <small class="text-muted">${flight.arrivalCity || ''}</small>
                        </div>
                    </div>
                </div>

                <div class="col-md-1 text-center">
                    <span class="badge ${flight.transfers === 0 ? 'bg-success' : 'bg-warning'} fs-6">
                        ${flight.transfers === 0 ? 'Прямой' : `${flight.transfers}`}
                    </span>
                </div>

                <div class="col-md-4">
                    <div class="d-flex align-items-center justify-content-end gap-3">
                        <button class="favorite-btn p-2 border-0 bg-transparent"
                                data-flight-id="${flightId}"
                                data-flight-data='${JSON.stringify(flightData).replace(/'/g, "&apos;")}'
                                title="${isAuth ? 'Добавить в избранное' : 'Войдите для сохранения'}"
                                style="transition: transform 0.2s;">
                            <i class="far fa-heart fa-lg text-muted"></i>
                        </button>

                        <div class="text-end">
                            <div class="d-flex align-items-baseline justify-content-end">
                                <h3 class="text-${typeColor} mb-0">${priceFormatted}</h3>
                                <span class="text-${typeColor} ms-1">${currency}</span>
                            </div>
                            <small class="text-muted d-block">за пассажира</small>
                            ${buyButtonHtml}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function buildStableFlightId(flight, isReturnFlight) {
    const airline = (flight.airlineCode || flight.airline || 'FLT').replace(/[^a-zA-Z0-9]/g, '');
    const flightNum = (flight.flightNumber || '000').replace(/[^a-zA-Z0-9]/g, '');
    const from = (flight.departureCity || 'FROM').replace(/[^a-zA-Z0-9]/g, '');
    const to = (flight.arrivalCity || 'TO').replace(/[^a-zA-Z0-9]/g, '');
    const departureDate = flight.departureTime ? new Date(flight.departureTime).toISOString().split('T')[0].replace(/-/g, '') : '000000';

    const baseId = `${airline}_${flightNum}_${from}_${to}_${departureDate}_${isReturnFlight ? 'R' : 'O'}`;

    let hash = 0;
    for (let i = 0; i < baseId.length; i++) {
        hash = ((hash << 5) - hash) + baseId.charCodeAt(i);
        hash = hash & hash;
    }

    return `FLT_${Math.abs(hash).toString(36)}_${isReturnFlight ? 'R' : 'O'}`;
}

// ==================== ПОКУПКА БИЛЕТОВ ====================
function selectRealFlight(flightId, price, airline, isReturn) {
    console.log('========== НАЧАЛО БРОНИРОВАНИЯ ==========');
    console.log('Параметры вызова:', { flightId, price, airline, isReturn });

    // Получаем данные поиска из формы
    const departureInput = document.getElementById('departureCity');
    const arrivalInput = document.getElementById('arrivalCity');
    const departureDateInput = document.getElementById('departureDate');
    const returnDateInput = document.getElementById('returnDate');
    const passengersSelect = document.getElementById('passengers');

    const isRoundTrip = returnDateInput && returnDateInput.value && returnDateInput.value.length > 0;

    console.log('isRoundTrip:', isRoundTrip);
    console.log('isReturn (это обратный рейс?):', isReturn);
    console.log('returnDate:', returnDateInput?.value);

    // Функция для форматирования даты для сервера
    const formatDateTimeForServer = (date) => {
        if (!date) return '';
        const d = new Date(date);
        const year = d.getFullYear();
        const month = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        const hours = String(d.getHours()).padStart(2, '0');
        const minutes = String(d.getMinutes()).padStart(2, '0');
        const seconds = String(d.getSeconds()).padStart(2, '0');
        return `${year}-${month}-${day}T${hours}:${minutes}:${seconds}`;
    };

    // Функция для парсинга города из строки "Город (CODE)"
    const parseCity = (input) => {
        if (!input) return '';
        const match = input.match(/^([^(]+)/);
        return match ? match[1].trim() : input;
    };

    // Находим карточку текущего рейса
    let flightCard = null;
    if (event && event.currentTarget) {
        flightCard = event.currentTarget.closest('.flight-card');
    } else {
        // Если event нет, ищем по data-flight-id
        flightCard = document.querySelector(`.flight-card .favorite-btn[data-flight-id="${flightId}"]`)?.closest('.flight-card');
    }

    if (!flightCard) {
        console.error('❌ Карточка рейса не найдена');
        return;
    }

    const favoriteButton = flightCard.querySelector('.favorite-btn');
    if (!favoriteButton) {
        console.error('❌ Кнопка избранного не найдена');
        return;
    }

    const flightDataStr = favoriteButton.getAttribute('data-flight-data');
    if (!flightDataStr) {
        console.error('❌ Данные рейса не найдены');
        return;
    }

    try {
        const currentFlight = JSON.parse(flightDataStr.replace(/&apos;/g, "'"));
        console.log('✅ Текущий рейс:', currentFlight);

        const departureCity = parseCity(departureInput?.value || currentFlight.departureCity);
        const arrivalCity = parseCity(arrivalInput?.value || currentFlight.arrivalCity);

        // Базовые данные для рейса
        let departureDateTime = currentFlight.departureTime;
        let arrivalDateTime = currentFlight.arrivalTime;

        if (!departureDateTime || departureDateTime === 'Invalid Date') {
            if (departureDateInput && departureDateInput.value) {
                const dateStr = departureDateInput.value;
                const timeStr = currentFlight.departureTime ? new Date(currentFlight.departureTime).toTimeString().slice(0, 5) : '10:00';
                departureDateTime = new Date(`${dateStr}T${timeStr}`);
            } else {
                departureDateTime = new Date();
                departureDateTime.setDate(departureDateTime.getDate() + 1);
                departureDateTime.setHours(10, 0, 0, 0);
            }
        }

        if (!arrivalDateTime || arrivalDateTime === 'Invalid Date') {
            arrivalDateTime = new Date(departureDateTime);
            const duration = currentFlight.duration || 120;
            arrivalDateTime.setMinutes(departureDateTime.getMinutes() + duration);
        }

        // Базовый объект бронирования
        let bookingData = {
            flightId: currentFlight.flightId,
            airline: currentFlight.airline || airline,
            airlineCode: currentFlight.airlineCode || 'SU',
            airlineLogo: currentFlight.airlineLogo || '',
            flightNumber: currentFlight.flightNumber || 'SU 1234',
            departureCity: departureCity,
            arrivalCity: arrivalCity,
            departureAirport: currentFlight.departureAirport || '',
            arrivalAirport: currentFlight.arrivalAirport || '',
            departureDateTime: formatDateTimeForServer(departureDateTime),
            arrivalDateTime: formatDateTimeForServer(arrivalDateTime),
            price: price,
            duration: currentFlight.duration || 120,
            transfers: currentFlight.transfers || 0,
            aircraft: currentFlight.aircraft || 'Airbus A320',
            baggage: '1x23кг',
            handLuggage: '1x10кг',
            meal: 'Включено',
            flightClass: 'economy',
            isRoundTrip: isRoundTrip,
            passengers: parseInt(passengersSelect?.value || '1'),
            returnFlightId: null,
            returnAirline: null,
            returnFlightNumber: null,
            returnDepartureDateTime: null,
            returnArrivalDateTime: null,
            returnDuration: null,
            returnTransfers: null
        };

        // ========== ЕСЛИ ПОЕЗДКА ТУДА-ОБРАТНО ==========
        if (isRoundTrip) {
            console.log('🔍 Поиск парного рейса...');

            let partnerFlightCard = null;
            const allFlightCards = document.querySelectorAll('.flight-card');
            const isCurrentReturn = flightCard.classList.contains('return-flight');

            for (const card of allFlightCards) {
                if (card === flightCard) continue; // Пропускаем текущий рейс

                const cardIsReturn = card.classList.contains('return-flight');
                const btn = card.querySelector('.favorite-btn');

                if (btn) {
                    const dataStr = btn.getAttribute('data-flight-data');
                    if (dataStr) {
                        try {
                            const flight = JSON.parse(dataStr.replace(/&apos;/g, "'"));

                            // Если текущий рейс - обратный, ищем прямой
                            if (isCurrentReturn) {
                                // Ищем прямой рейс: откуда вылетаем обратно = куда прилетаем прямым
                                if (flight.departureCity === currentFlight.arrivalCity &&
                                    flight.arrivalCity === currentFlight.departureCity &&
                                    !cardIsReturn) {
                                    partnerFlightCard = card;
                                    console.log('✅ Найден прямой рейс (парный для обратного):', flight);
                                    break;
                                }
                            }
                            // Если текущий рейс - прямой, ищем обратный
                            else {
                                if (flight.departureCity === currentFlight.arrivalCity &&
                                    flight.arrivalCity === currentFlight.departureCity &&
                                    cardIsReturn) {
                                    partnerFlightCard = card;
                                    console.log('✅ Найден обратный рейс (парный для прямого):', flight);
                                    break;
                                }
                            }
                        } catch (e) {
                            console.error('Ошибка парсинга данных рейса:', e);
                        }
                    }
                }
            }

            // Если нашли парный рейс, добавляем его данные в бронирование
            if (partnerFlightCard) {
                const partnerFavoriteBtn = partnerFlightCard.querySelector('.favorite-btn');
                if (partnerFavoriteBtn) {
                    const partnerFlightDataStr = partnerFavoriteBtn.getAttribute('data-flight-data');
                    if (partnerFlightDataStr) {
                        const partnerFlight = JSON.parse(partnerFlightDataStr.replace(/&apos;/g, "'"));
                        console.log('✅ Данные парного рейса:', partnerFlight);

                        // Определяем, какой из рейсов прямой, а какой обратный
                        const isCurrentReturn = flightCard.classList.contains('return-flight');

                        if (isCurrentReturn) {
                            // Текущий - обратный, парный - прямой
                            // Прямой рейс (туда)
                            bookingData.flightId = partnerFlight.flightId;
                            bookingData.airline = partnerFlight.airline;
                            bookingData.airlineCode = partnerFlight.airlineCode;
                            bookingData.flightNumber = partnerFlight.flightNumber;
                            bookingData.departureCity = partnerFlight.departureCity;
                            bookingData.arrivalCity = partnerFlight.arrivalCity;
                            bookingData.departureAirport = partnerFlight.departureAirport;
                            bookingData.arrivalAirport = partnerFlight.arrivalAirport;
                            bookingData.departureDateTime = formatDateTimeForServer(partnerFlight.departureTime);
                            bookingData.arrivalDateTime = formatDateTimeForServer(partnerFlight.arrivalTime);
                            bookingData.duration = partnerFlight.duration;
                            bookingData.transfers = partnerFlight.transfers;
                            bookingData.aircraft = partnerFlight.aircraft;

                            // Обратный рейс
                            bookingData.returnFlightId = currentFlight.flightId;
                            bookingData.returnAirline = currentFlight.airline;
                            bookingData.returnFlightNumber = currentFlight.flightNumber;
                            bookingData.returnDepartureDateTime = formatDateTimeForServer(currentFlight.departureTime);
                            bookingData.returnArrivalDateTime = formatDateTimeForServer(currentFlight.arrivalTime);
                            bookingData.returnDuration = currentFlight.duration;
                            bookingData.returnTransfers = currentFlight.transfers;
                        } else {
                            // Текущий - прямой, парный - обратный
                            // Обратный рейс
                            bookingData.returnFlightId = partnerFlight.flightId;
                            bookingData.returnAirline = partnerFlight.airline;
                            bookingData.returnFlightNumber = partnerFlight.flightNumber;
                            bookingData.returnDepartureDateTime = formatDateTimeForServer(partnerFlight.departureTime);
                            bookingData.returnArrivalDateTime = formatDateTimeForServer(partnerFlight.arrivalTime);
                            bookingData.returnDuration = partnerFlight.duration;
                            bookingData.returnTransfers = partnerFlight.transfers;
                        }

                        console.log('✅ Парный рейс добавлен в бронирование');
                    }
                }
            } else {
                console.warn('⚠️ Не найден парный рейс!');
                // Если не нашли парный рейс, но isRoundTrip=true - показываем ошибку
                if (isRoundTrip) {
                    showNotification('Пожалуйста, выберите оба рейса (туда и обратно) для бронирования.', 'warning');
                    return;
                }
            }
        }

        // Проверка: если isRoundTrip=true, но нет данных обратного рейса - ошибка
        if (isRoundTrip && !bookingData.returnFlightNumber) {
            console.error('❌ ОШИБКА: Поездка туда-обратно, но нет данных обратного рейса!');
            showNotification('Ошибка: Не найден обратный рейс. Пожалуйста, убедитесь, что вы выбрали оба рейса (туда и обратно).', 'danger');
            return;
        }

        console.log('✅ Итоговые данные для бронирования:', bookingData);

        // Формируем URL
        const params = new URLSearchParams();
        for (const [key, value] of Object.entries(bookingData)) {
            if (value !== null && value !== undefined && value !== '') {
                params.append(key, value.toString());
            }
        }

        const url = `/FlightBooking/Book?${params.toString()}`;
        console.log('✅ URL для перехода:', url);
        console.log('========== КОНЕЦ БРОНИРОВАНИЯ ==========');

        window.location.href = url;

    } catch (error) {
        console.error('❌ Ошибка при подготовке данных:', error);
        showNotification('Ошибка при подготовке данных для бронирования: ' + error.message, 'danger');
    }
}

// ========== ВСПОМОГАТЕЛЬНАЯ ФУНКЦИЯ ДЛЯ ПОИСКА ОБРАТНОГО РЕЙСА ==========
function findReturnFlightCard(departureFlight) {
    const allFlightCards = document.querySelectorAll('.flight-card');

    for (const card of allFlightCards) {
        // Ищем рейс обратно (с классом return-flight и противоположным маршрутом)
        if (card.classList.contains('return-flight')) {
            const favoriteBtn = card.querySelector('.favorite-btn');
            if (favoriteBtn) {
                const flightDataStr = favoriteBtn.getAttribute('data-flight-data');
                if (flightDataStr) {
                    try {
                        const flight = JSON.parse(flightDataStr.replace(/&apos;/g, "'"));
                        // Проверяем, что это обратный маршрут (прилетаем туда, откуда вылетали)
                        if (flight.departureCity === departureFlight.arrivalCity &&
                            flight.arrivalCity === departureFlight.departureCity) {
                            return card;
                        }
                    } catch (e) {
                        console.error('Ошибка парсинга данных рейса:', e);
                    }
                }
            }
        }
    }
    return null;
}
// ==================== МОИ ЗАКАЗЫ ====================
function showMyOrders() {
    if (!isUserAuthenticated) {
        showAuthRequiredModal();
        return;
    }

    fetch('/api/flights/my-orders', { credentials: 'include' })
        .then(response => {
            if (response.ok) return response.json();
            throw new Error('Не удалось загрузить заказы');
        })
        .then(data => displayOrdersModal(data.orders || []))
        .catch(error => {
            console.error('Ошибка при получении заказов:', error);
            alert('Ошибка при загрузке заказов');
        });
}

function displayOrdersModal(orders) {
    let ordersHtml = '';
    if (orders.length === 0) {
        ordersHtml = `
            <div class="text-center py-5">
                <i class="fas fa-ticket-alt fa-4x text-muted mb-3"></i>
                <h4>У вас пока нет заказов</h4>
                <p class="text-muted">Начните поиск рейсов и сделайте свой первый заказ!</p>
                <button class="btn btn-primary mt-3" data-bs-dismiss="modal">Найти рейсы</button>
            </div>
        `;
    } else {
        ordersHtml = orders.map(order => `
            <div class="card mb-3">
                <div class="card-header d-flex justify-content-between align-items-center">
                    <div>
                        <strong>Заказ #${order.orderNumber}</strong>
                        <span class="badge ${getStatusBadgeClass(order.status)} ms-2">${getStatusText(order.status)}</span>
                    </div>
                    <small class="text-muted">${new Date(order.createdAt).toLocaleDateString('ru-RU')}</small>
                </div>
                <div class="card-body">
                    <div class="row">
                        <div class="col-md-6">
                            <p class="mb-1"><strong>Рейс:</strong> ${order.flightNumber}</p>
                            <p class="mb-1"><strong>Маршрут:</strong> ${order.departureCity} → ${order.arrivalCity}</p>
                            <p class="mb-1"><strong>Вылет:</strong> ${new Date(order.departureTime).toLocaleString('ru-RU')}</p>
                        </div>
                        <div class="col-md-6">
                            <p class="mb-1"><strong>Цена:</strong> ${order.price.toLocaleString('ru-RU')} ${order.currency}</p>
                            <p class="mb-1"><strong>Пассажиров:</strong> ${order.passengers?.length || 1}</p>
                            <p class="mb-1"><strong>Билет:</strong> ${order.ticketNumber || 'ожидает выписки'}</p>
                        </div>
                    </div>
                    <div class="mt-3">
                        <button class="btn btn-sm btn-outline-primary me-2" onclick="viewOrderDetails('${order.id}')">
                            <i class="fas fa-eye me-1"></i>Подробнее
                        </button>
                        ${order.status === 'confirmed' ? `
                            <button class="btn btn-sm btn-outline-success" onclick="printDemoTicket('${order.ticketNumber || 'DEMO-001'}')">
                                <i class="fas fa-print me-1"></i>Печать билета
                            </button>
                        ` : ''}
                        ${order.status === 'pending' ? `
                            <button class="btn btn-sm btn-outline-danger" onclick="cancelOrder('${order.id}')">
                                <i class="fas fa-times me-1"></i>Отменить
                            </button>
                        ` : ''}
                    </div>
                </div>
            </div>
        `).join('');
    }

    const modalHtml = `
        <div class="modal fade" id="ordersModal" tabindex="-1">
            <div class="modal-dialog modal-lg modal-dialog-centered">
                <div class="modal-content">
                    <div class="modal-header bg-primary text-white">
                        <h5 class="modal-title">
                            <i class="fas fa-history me-2"></i>
                            Мои заказы (${orders.length})
                        </h5>
                        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">${ordersHtml}</div>
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', modalHtml);
    const ordersModal = new bootstrap.Modal(document.getElementById('ordersModal'));
    ordersModal.show();

    document.getElementById('ordersModal').addEventListener('hidden.bs.modal', function () {
        this.remove();
    });
}

function getStatusBadgeClass(status) {
    switch (status) {
        case 'confirmed': return 'bg-success';
        case 'pending': return 'bg-warning';
        case 'cancelled': return 'bg-danger';
        default: return 'bg-secondary';
    }
}

function getStatusText(status) {
    switch (status) {
        case 'confirmed': return 'Подтвержден';
        case 'pending': return 'Ожидает оплаты';
        case 'cancelled': return 'Отменен';
        default: return status;
    }
}

function viewOrderDetails(orderId) {
    fetch(`/api/flights/order/${orderId}`, { credentials: 'include' })
        .then(response => {
            if (response.ok) return response.json();
            throw new Error('Не удалось загрузить детали заказа');
        })
        .then(data => showOrderDetailsModal(data.order))
        .catch(error => console.error('Ошибка при получении деталей заказа:', error));
}

function showOrderDetailsModal(order) {
    const passengersHtml = order.passengers.map(p => `
        <tr>
            <td>${p.lastName} ${p.firstName} ${p.middleName || ''}</td>
            <td>${new Date(p.dateOfBirth).toLocaleDateString('ru-RU')}</td>
            <td>${p.documentNumber}</td>
            <td>${p.seatNumber || '-'}</td>
        </tr>
    `).join('');

    const modalHtml = `
        <div class="modal fade" id="orderDetailsModal" tabindex="-1">
            <div class="modal-dialog modal-lg modal-dialog-centered">
                <div class="modal-content">
                    <div class="modal-header bg-info text-white">
                        <h5 class="modal-title">
                            <i class="fas fa-file-invoice me-2"></i>
                            Заказ #${order.orderNumber}
                        </h5>
                        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        <div class="row mb-4">
                            <div class="col-md-6">
                                <h6>Информация о рейсе</h6>
                                <p><strong>Авиакомпания:</strong> ${order.airline}</p>
                                <p><strong>Рейс:</strong> ${order.flightNumber}</p>
                                <p><strong>Вылет:</strong> ${new Date(order.departureTime).toLocaleString('ru-RU')}</p>
                                <p><strong>Прилет:</strong> ${new Date(order.arrivalTime).toLocaleString('ru-RU')}</p>
                            </div>
                            <div class="col-md-6">
                                <h6>Детали заказа</h6>
                                <p><strong>Статус:</strong> <span class="badge ${getStatusBadgeClass(order.status)}">${getStatusText(order.status)}</span></p>
                                <p><strong>Создан:</strong> ${new Date(order.createdAt).toLocaleString('ru-RU')}</p>
                                <p><strong>Билет:</strong> ${order.ticketNumber || '—'}</p>
                                <p><strong>Сумма:</strong> ${order.price.toLocaleString('ru-RU')} ${order.currency}</p>
                            </div>
                        </div>

                        <h6>Пассажиры</h6>
                        <div class="table-responsive">
                            <table class="table table-striped">
                                <thead>
                                    <tr>
                                        <th>ФИО</th>
                                        <th>Дата рождения</th>
                                        <th>Документ</th>
                                        <th>Место</th>
                                    </tr>
                                </thead>
                                <tbody>${passengersHtml}</tbody>
                            </table>
                        </div>

                        <div class="alert alert-info mt-3">
                            <i class="fas fa-info-circle me-2"></i>
                            Демо-заказ. Все данные сгенерированы автоматически.
                        </div>
                    </div>
                    <div class="modal-footer">
                        ${order.status === 'confirmed' && order.ticketNumber ? `
                            <button class="btn btn-primary" onclick="printDemoTicket('${order.ticketNumber}')">
                                <i class="fas fa-print me-1"></i>Печать билета
                            </button>
                        ` : ''}
                        <button class="btn btn-secondary" data-bs-dismiss="modal">Закрыть</button>
                    </div>
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', modalHtml);
    const modal = new bootstrap.Modal(document.getElementById('orderDetailsModal'));
    modal.show();

    document.getElementById('orderDetailsModal').addEventListener('hidden.bs.modal', function () {
        this.remove();
    });
}

function cancelOrder(orderId) {
    if (!confirm('Вы уверены, что хотите отменить этот заказ?')) return;

    fetch(`/api/flights/order/${orderId}/cancel`, {
        method: 'POST',
        credentials: 'include'
    })
        .then(response => {
            if (response.ok) return response.json();
            throw new Error('Не удалось отменить заказ');
        })
        .then(data => {
            if (data.success) {
                alert('Заказ успешно отменен!');
                bootstrap.Modal.getInstance(document.getElementById('ordersModal')).hide();
                setTimeout(() => showMyOrders(), 300);
            }
        })
        .catch(error => {
            console.error('Ошибка при отмене заказа:', error);
            alert('Не удалось отменить заказ');
        });
}

// ==================== ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ====================
function extractCityName(fullString) {
    if (!fullString) return '';
    let cityName = fullString.replace(/\([^)]*\)/g, '').trim();
    return cityName || fullString;
}

function formatTime(dateTimeString) {
    if (!dateTimeString) return '--:--';
    try {
        let date = typeof dateTimeString === 'string' ? new Date(dateTimeString) : dateTimeString;
        if (isNaN(date.getTime())) return '--:--';
        // Используем локальное время без смещения
        const hours = date.getHours().toString().padStart(2, '0');
        const minutes = date.getMinutes().toString().padStart(2, '0');
        return `${hours}:${minutes}`;
    } catch (error) {
        console.error('Ошибка форматирования времени:', error, dateTimeString);
        return '--:--';
    }
}

function formatDateForDisplay(dateString) {
    if (!dateString) return '';
    try {
        const date = new Date(dateString);
        if (isNaN(date.getTime())) return '';
        return date.toLocaleDateString('ru-RU', {
            weekday: 'short',
            day: 'numeric',
            month: 'long',
            year: 'numeric'
        });
    } catch (error) {
        console.error('Ошибка форматирования даты:', error, dateString);
        return '';
    }
}

function formatDateForApi(date) {
    if (!date) return '';
    try {
        const d = new Date(date);
        return d.toISOString().split('T')[0];
    } catch (error) {
        console.error('Ошибка форматирования даты для API:', error);
        return '';
    }
}

function showAuthRequiredModal() {
    if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
        const modalElement = document.getElementById('authRequiredModal');
        if (modalElement) {
            const modal = new bootstrap.Modal(modalElement);
            modal.show();
        }
    } else {
        alert('Для сохранения рейсов в избранное необходимо авторизоваться.\n\nПерейдите на страницу входа или регистрации.');
        window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname);
    }
}

function showNotification(message, type = 'info') {
    const oldNotifications = document.querySelectorAll('.notification-alert');
    oldNotifications.forEach(notification => notification.remove());

    const notification = document.createElement('div');
    notification.className = `notification-alert alert alert-${type} alert-dismissible fade show position-fixed`;
    notification.style.cssText = `
        top: 20px;
        right: 20px;
        z-index: 9999;
        min-width: 300px;
    `;

    notification.innerHTML = `
        <div class="d-flex align-items-center">
            <i class="fas ${type === 'success' ? 'fa-check-circle' : 'fa-info-circle'} me-2"></i>
            <div>${message}</div>
            <button type="button" class="btn-close ms-auto" data-bs-dismiss="alert"></button>
        </div>
    `;

    document.body.appendChild(notification);
    setTimeout(() => {
        if (notification.parentElement) notification.remove();
    }, 5000);
}

function debounceSearch(input, dropdown, delay = 300) {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => {
        searchCitiesFromTravelPayouts(input.value, dropdown);
    }, delay);
}

function loadPopularDestinations() {
    try {
        setTimeout(() => {
            const popularDestinationsHTML = `
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Москва', 'Санкт-Петербург')">
                        <img src="https://i.pinimg.com/originals/8e/d6/12/8ed6120ddbb569d44c6c7edaea15cce9.png" class="card-img-top" alt="Москва - Санкт-Петербург">
                        <div class="card-body">
                            <h5 class="card-title">Москва → Санкт-Петербург</h5>
                            <p class="card-text">От 2 500 ₽</p>
                            <p class="text-muted small">В пути от 1ч 30м</p>
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Москва', 'Сочи')">
                        <img src="https://upload.wikimedia.org/wikipedia/commons/thumb/2/28/Sochi_Park_Night.jpg/960px-Sochi_Park_Night.jpg"
                             class="card-img-top"
                             alt="Сочи Парк ночью">
                        <div class="card-body">
                            <h5 class="card-title">Москва → Сочи</h5>
                            <p class="card-text">От 4 800 ₽</p>
                            <p class="text-muted small">В пути от 2ч 30м</p>
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Москва', 'Казань')">
                        <img src="https://cdn.culture.ru/images/a95b2c46-77db-5224-a88b-1079b9f3c3b0" class="card-img-top" alt="Москва - Казань">
                        <div class="card-body">
                            <h5 class="card-title">Москва → Казань</h5>
                            <p class="card-text">От 3 200 ₽</p>
                            <p class="text-muted small">В пути от 1ч 45м</p>
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Москва', 'Екатеринбург')">
                        <img src="https://photobooth.cdn.sports.ru/preset/post/7/0e/3974000c34c258259e1a56a9ab84b.jpeg?f=webp&q=90&s=2x&w=730" class="card-img-top" alt="Москва - Екатеринбург">
                        <div class="card-body">
                            <h5 class="card-title">Москва → Екатеринбург</h5>
                            <p class="card-text">От 3 800 ₽</p>
                            <p class="text-muted small">В пути от 2ч 15м</p>
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Москва', 'Краснодар')">
                        <img src="https://blog.ostrovok.ru/wp-content/uploads/2022/05/8-1.jpg" class="card-img-top" alt="Москва - Краснодар">
                        <div class="card-body">
                            <h5 class="card-title">Москва → Краснодар</h5>
                            <p class="card-text">От 4 200 ₽</p>
                            <p class="text-muted small">В пути от 2ч</p>
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Тюмень', 'Москва')">
                        <img src="https://upload.wikimedia.org/wikipedia/commons/thumb/8/85/Saint_Basil%27s_Cathedral_and_the_Red_Square.jpg/960px-Saint_Basil%27s_Cathedral_and_the_Red_Square.jpg" class="card-img-top" alt="Тюмень - Москва">
                        <div class="card-body">
                            <h5 class="card-title">Тюмень → Москва</h5>
                            <p class="card-text">От 5 500 ₽</p>
                            <p class="text-muted small">В пути от 2ч 30м</p>
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Москва', 'Калининград')">
                        <img src="https://fs.tonkosti.ru/sized/c960x400/7y/4s/7y4s5zdc88oww4sskoswcg4co.jpg" class="card-img-top" alt="Калининград - Кафедральный собор">
                        <div class="card-body">
                            <h5 class="card-title">Москва → Калининград</h5>
                            <p class="card-text">От 5 900 ₽</p>
                            <p class="text-muted small">В пути от 2ч 10м</p>
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Москва', 'Новосибирск')">
                        <img src="https://avatars.mds.yandex.net/i?id=3c8e069f6919ddc94fd06875b3bb9958_l-10754966-images-thumbs&n=13" class="card-img-top" alt="Новосибирск - Театр оперы и балета">
                        <div class="card-body">
                            <h5 class="card-title">Москва → Новосибирск</h5>
                            <p class="card-text">От 6 500 ₽</p>
                            <p class="text-muted small">В пути от 3ч 25м</p>
                        </div>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="card destination-card h-100" onclick="selectPopularDestination('Москва', 'Владивосток')">
                        <img src="https://i.ytimg.com/vi/xQtLDcSFjJ0/maxresdefault.jpg" class="card-img-top" alt="Владивосток - Золотой мост">
                        <div class="card-body">
                            <h5 class="card-title">Москва → Владивосток</h5>
                            <p class="card-text">От 9 500 ₽</p>
                            <p class="text-muted small">В пути от 7ч 50м</p>
                        </div>
                    </div>
                </div>
            `;

            const container = document.getElementById('popularDestinations');
            if (container) container.innerHTML = popularDestinationsHTML;
        }, 1000);
    } catch (error) {
        console.error('❌ Ошибка загрузки популярных направлений:', error);
    }
}

function selectPopularDestination(fromCity, toCity) {
    console.log(`🎯 Выбрано направление: ${fromCity} → ${toCity}`);
    document.getElementById('departureCity').value = `${fromCity} (${getCityCode(fromCity)})`;
    document.getElementById('arrivalCity').value = `${toCity} (${getCityCode(toCity)})`;

    document.getElementById('flightSearchForm').scrollIntoView({ behavior: 'smooth' });
    showNotification(`Направление ${fromCity} → ${toCity} добавлено в форму поиска`, 'success');
}

function getCityCode(cityName) {
    const cityCodes = {
        "Москва": "MOW", "Санкт-Петербург": "LED", "Сочи": "AER", "Казань": "KZN",
        "Екатеринбург": "SVX", "Краснодар": "KRR", "Минеральные Воды": "MRV",
        "Симферополь": "SIP", "Калининград": "KGD", "Новосибирск": "OVB", "Тюмень": "TJM"
    };
    return cityCodes[cityName] || "---";
}
// ==================== ОБРАБОТКА РЕЙСОВ ИЗ API ====================
function processApiFlights(flights, searchData) {
    if (!flights || flights.length === 0) return [];

    const hasReturnDate = searchData.returnDate && searchData.returnDate !== null && searchData.returnDate !== '';

    // Разделяем рейсы на туда и обратно на основе городов и дат
    const departureFlights = [];
    const returnFlights = [];

    const departureDateStr = searchData.departureDate;
    const returnDateStr = searchData.returnDate;

    for (const flight of flights) {
        // Определяем, является ли рейс обратным
        const flightDate = new Date(flight.departureTime).toISOString().split('T')[0];

        // Если есть дата возврата и дата рейса совпадает с датой возврата,
        // и направление обратное (откуда прилетели туда - туда и вылетаем обратно)
        if (hasReturnDate && flightDate === returnDateStr &&
            flight.departureCity === searchData.arrivalCity &&
            flight.arrivalCity === searchData.departureCity) {
            flight.isReturn = true;
            returnFlights.push(flight);
        }
        // Иначе это рейс туда
        else if (flightDate === departureDateStr &&
            flight.departureCity === searchData.departureCity &&
            flight.arrivalCity === searchData.arrivalCity) {
            flight.isReturn = false;
            departureFlights.push(flight);
        }
        // Если рейс на другую дату - пропускаем
        else {
            console.log(`Рейс на другую дату: ${flightDate}, ожидалось: ${departureDateStr} или ${returnDateStr}`);
        }
    }

    console.log(`Найдено рейсов туда: ${departureFlights.length}`);
    console.log(`Найдено рейсов обратно: ${returnFlights.length}`);

    // Объединяем и возвращаем (сначала туда, потом обратно)
    return [...departureFlights, ...returnFlights];
}
// ==================== ИНИЦИАЛИЗАЦИЯ ====================
async function initializeFlightPage() {
    console.log('Инициализация страницы авиабилетов...');

    const departureInput = document.getElementById('departureCity');
    const arrivalInput = document.getElementById('arrivalCity');
    const departureDropdown = document.getElementById('departureDropdown');
    const arrivalDropdown = document.getElementById('arrivalDropdown');
    const departureDateInput = document.getElementById('departureDate');
    const returnDateInput = document.getElementById('returnDate');

    await checkAuthStatus();

    if (isUserAuthenticated) {
        await loadFavorites();
        console.log('Избранное загружено после авторизации');
    }

    if (departureInput && departureDropdown) {
        departureInput.addEventListener('input', () => {
            currentDropdown = departureDropdown;
            currentInput = departureInput;
            debounceSearch(departureInput, departureDropdown);
        });

        departureInput.addEventListener('focus', () => {
            if (departureInput.value.length >= 2) {
                currentDropdown = departureDropdown;
                currentInput = departureInput;
                searchCitiesFromTravelPayouts(departureInput.value, departureDropdown);
            }
        });

        departureInput.addEventListener('blur', () => {
            setTimeout(() => {
                if (!departureDropdown.contains(document.activeElement)) {
                    departureDropdown.style.display = 'none';
                }
            }, 200);
        });
    }

    if (arrivalInput && arrivalDropdown) {
        arrivalInput.addEventListener('input', () => {
            currentDropdown = arrivalDropdown;
            currentInput = arrivalInput;
            debounceSearch(arrivalInput, arrivalDropdown);
        });

        arrivalInput.addEventListener('focus', () => {
            if (arrivalInput.value.length >= 2) {
                currentDropdown = arrivalDropdown;
                currentInput = arrivalInput;
                searchCitiesFromTravelPayouts(arrivalInput.value, arrivalDropdown);
            }
        });

        arrivalInput.addEventListener('blur', () => {
            setTimeout(() => {
                if (!arrivalDropdown.contains(document.activeElement)) {
                    arrivalDropdown.style.display = 'none';
                }
            }, 200);
        });
    }

    document.addEventListener('click', (e) => {
        if (!e.target.closest('.city-autocomplete')) {
            if (departureDropdown) departureDropdown.style.display = 'none';
            if (arrivalDropdown) arrivalDropdown.style.display = 'none';
        }
    });

    document.addEventListener('keydown', (e) => {
        if (!currentDropdown || currentDropdown.style.display === 'none') return;

        const items = currentDropdown.querySelectorAll('.autocomplete-item');
        let activeItem = currentDropdown.querySelector('.autocomplete-item.active');
        let activeIndex = activeItem ? Array.from(items).indexOf(activeItem) : -1;

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                if (activeIndex < items.length - 1) {
                    if (activeItem) activeItem.classList.remove('active');
                    items[activeIndex + 1].classList.add('active');
                } else if (items.length > 0 && activeIndex === -1) {
                    items[0].classList.add('active');
                }
                break;

            case 'ArrowUp':
                e.preventDefault();
                if (activeIndex > 0) {
                    if (activeItem) activeItem.classList.remove('active');
                    items[activeIndex - 1].classList.add('active');
                }
                break;

            case 'Enter':
                e.preventDefault();
                if (activeItem) {
                    activeItem.click();
                }
                break;

            case 'Escape':
                currentDropdown.style.display = 'none';
                break;
        }
    });

    if (departureDateInput) {
        const today = new Date().toISOString().split('T')[0];
        departureDateInput.min = today;
    }

    if (returnDateInput) {
        const tomorrow = new Date();
        tomorrow.setDate(tomorrow.getDate() + 1);
        returnDateInput.min = tomorrow.toISOString().split('T')[0];
    }

    if (departureDateInput && returnDateInput) {
        departureDateInput.addEventListener('change', function () {
            const departureDate = new Date(this.value);
            const nextDay = new Date(departureDate);
            nextDay.setDate(departureDate.getDate() + 1);

            const minReturnDate = nextDay.toISOString().split('T')[0];
            returnDateInput.min = minReturnDate;

            if (returnDateInput.value && returnDateInput.value < minReturnDate) {
                returnDateInput.value = minReturnDate;
            }
        });
    }

    const flightSearchForm = document.getElementById('flightSearchForm');
    if (flightSearchForm) {
        flightSearchForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            console.log('=== ОТПРАВКА ФОРМЫ ===');

            const departure = document.getElementById('departureCity').value;
            const arrival = document.getElementById('arrivalCity').value;
            const departureDate = document.getElementById('departureDate').value;
            const returnDate = document.getElementById('returnDate').value;
            const passengers = document.getElementById('passengers').value;

            if (!departure || !arrival) {
                showNotification('Пожалуйста, заполните города вылета и прилета', 'warning');
                return;
            }

            if (!departureDate) {
                showNotification('Пожалуйста, выберите дату вылета', 'warning');
                return;
            }

            const departureCity = extractCityName(departure);
            const arrivalCity = extractCityName(arrival);

            const searchData = {
                departureCity: departureCity,
                arrivalCity: arrivalCity,
                departureDate: departureDate,
                passengers: parseInt(passengers),
                class: "economy",
                tripType: returnDate && returnDate.length > 0 ? "round" : "oneway"
            };

            if (returnDate && returnDate.length > 0) {
                searchData.returnDate = returnDate;
            }

            console.log('Параметры поиска:', searchData);

            const submitBtn = this.querySelector('button[type="submit"]');
            const originalText = submitBtn.innerHTML;
            submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Поиск...';
            submitBtn.disabled = true;

            try {
                const result = await searchFlights(searchData);
                if (result.success) {
                    showFlightResults(result.flights, searchData);
                    showNotification(`Найдено ${result.flights?.length || 0} рейсов`, 'success');
                } else {
                    showNotification(result.error || 'Произошла ошибка при поиске', 'danger');
                }
            } catch (error) {
                console.error('Ошибка поиска:', error);
                showNotification(`Ошибка: ${error.message}`, 'danger');
            } finally {
                submitBtn.innerHTML = originalText;
                submitBtn.disabled = false;
            }
        });
    }

    loadPopularDestinations();

    window.addEventListener('focus', () => {
        if (isUserAuthenticated) {
            console.log('Страница в фокусе, проверяем избранное...');
            loadFavorites();
        }
    });

    console.log('Инициализация страницы авиабилетов завершена');
}

// ==================== ГЛОБАЛЬНЫЕ ОБЪЯВЛЕНИЯ ====================
window.toggleFavorite = toggleFavorite;
window.selectRealFlight = selectRealFlight;
window.selectPopularDestination = selectPopularDestination;
window.checkAuthStatus = checkAuthStatus;
window.handleFavoriteClick = handleFavoriteClick;
window.showMyOrders = showMyOrders;
window.viewOrderDetails = viewOrderDetails;
window.cancelOrder = cancelOrder;

// ==================== ЗАГРУЗКА СТРАНИЦЫ ====================
document.addEventListener('DOMContentLoaded', function () {
    console.log('Инициализация страницы авиабилетов...');
    initializeFlightPage();
});