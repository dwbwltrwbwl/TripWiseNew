// Глобальные переменные
let stationsData = [];
let isUserAuthenticated = false;
let userId = null;
let favoriteTrains = new Set();


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
                await loadFavoriteTrains();
            }

            // Обновляем кнопки покупки при изменении статуса авторизации
            if (wasAuthenticated !== isUserAuthenticated) {
                setTimeout(() => {
                    updateAllBuyButtons();
                }, 300);
            }
        }
    } catch (error) {
        console.error('Ошибка проверки авторизации:', error);
        isUserAuthenticated = false;
        userId = null;
    }
}

// ==================== ФУНКЦИИ ИЗБРАННОГО ====================
async function loadFavoriteTrains() {
    try {
        console.log('Загрузка избранных поездов...');
        const response = await fetch('/api/favorites/train/list', {
            credentials: 'include',
            headers: { 'Accept': 'application/json' }
        });

        if (response.ok) {
            const data = await response.json();
            if (data.success && data.favorites) {
                favoriteTrains = new Set(data.favorites);
                console.log('Загружено избранных поездов:', Array.from(favoriteTrains));
                updateAllTrainFavoriteButtons();
            }
        } else {
            console.error('Ошибка загрузки избранных поездов:', response.status);
        }
    } catch (error) {
        console.error('Ошибка загрузки избранных поездов:', error);
    }
}

async function toggleTrainFavorite(trainGroupId, trainData) {
    if (!isUserAuthenticated) {
        showAuthModal();
        return;
    }

    // ДОБАВЬТЕ ЭТО ЛОГИРОВАНИЕ
    console.log('=== ОТПРАВКА В ИЗБРАННОЕ ===');
    console.log('trainGroupId:', trainGroupId);
    console.log('departureDateTime (отправляется):', trainData.departureDateTime);
    console.log('arrivalDateTime (отправляется):', trainData.arrivalDateTime);
    console.log('Исходная дата из формы:', document.getElementById('railwayDepartureDate')?.value);

    const button = document.querySelector(`[data-train-group-id="${CSS.escape(trainGroupId)}"]`);
    if (button) {
        button.style.pointerEvents = 'none';
        button.style.opacity = '0.6';
    }

    try {
        const isCurrentlyFavorite = favoriteTrains.has(trainGroupId);
        const url = isCurrentlyFavorite ? '/api/favorites/train/remove' : '/api/favorites/train/add';

        console.log('Отправка запроса:', url);
        console.log('Данные:', JSON.stringify(trainData, null, 2));

        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            credentials: 'include',
            body: JSON.stringify(isCurrentlyFavorite ?
                { trainGroupId: trainGroupId } :
                trainData
            )
        });

        console.log('Статус ответа:', response.status);

        if (!response.ok) {
            const errorText = await response.text();
            console.error('Ошибка ответа:', errorText);
            throw new Error(`HTTP ошибка: ${response.status}`);
        }

        const result = await response.json();
        console.log('Результат операции:', result);

        if (result.success) {
            if (isCurrentlyFavorite) {
                favoriteTrains.delete(trainGroupId);
                showNotification('Поезд удален из избранного', 'success');
            } else {
                favoriteTrains.add(trainGroupId);
                showNotification('Поезд добавлен в избранное!', 'success');
            }
            updateTrainFavoriteButton(trainGroupId);
        } else {
            showNotification(result.message || 'Ошибка при сохранении', 'danger');
        }
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Ошибка при сохранении поезда: ' + error.message, 'danger');
    } finally {
        if (button) {
            button.style.pointerEvents = '';
            button.style.opacity = '';
        }
    }
}

function updateTrainFavoriteButton(trainGroupId) {
    const buttons = document.querySelectorAll(`[data-train-group-id="${CSS.escape(trainGroupId)}"]`);
    const isFavorite = favoriteTrains.has(trainGroupId);

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

function updateAllTrainFavoriteButtons() {
    const buttons = document.querySelectorAll('.train-favorite-btn');
    buttons.forEach(button => {
        const trainGroupId = button.getAttribute('data-train-group-id');
        if (trainGroupId) {
            updateTrainFavoriteButton(trainGroupId);
        }
    });
}

function handleTrainFavoriteClick(button) {
    if (button.disabled) return;
    button.disabled = true;

    const trainGroupId = button.getAttribute('data-train-group-id');
    const trainDataStr = button.getAttribute('data-train-data');

    if (!trainDataStr || !trainGroupId) {
        console.error('Данные поезда не найдены');
        button.disabled = false;
        return;
    }

    try {
        const trainData = JSON.parse(trainDataStr.replace(/&apos;/g, "'"));

        console.log('=== ДАННЫЕ ДЛЯ ИЗБРАННОГО ===');
        console.log('departureDateTime RAW:', trainData.departureDateTime);
        console.log('departureDateTime как локальная дата:', new Date(trainData.departureDateTime).toLocaleString('ru-RU'));

        toggleTrainFavorite(trainGroupId, trainData);
    } catch (error) {
        console.error('Ошибка парсинга данных:', error);
        showNotification('Ошибка при обработке данных', 'danger');
        button.disabled = false;
    } finally {
        setTimeout(() => { button.disabled = false; }, 500);
    }
}

function showAuthModal() {
    if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
        const modalElement = document.getElementById('authModal');
        if (modalElement) {
            const modal = new bootstrap.Modal(modalElement);
            modal.show();
        } else {
            // Создаем модальное окно, если его нет
            const modalHtml = `
                    <div class="modal fade" id="authModal" tabindex="-1">
                        <div class="modal-dialog modal-dialog-centered">
                            <div class="modal-content">
                                <div class="modal-header">
                                    <h5 class="modal-title">Требуется авторизация</h5>
                                    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                                </div>
                                <div class="modal-body">
                                    <p>Чтобы добавлять поезда в избранное, необходимо войти в систему.</p>
                                    <p>У вас еще нет аккаунта? <a href="/Account/Register">Зарегистрируйтесь</a></p>
                                </div>
                                <div class="modal-footer">
                                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Отмена</button>
                                    <a href="/Account/Login" class="btn btn-primary">Войти</a>
                                </div>
                            </div>
                        </div>
                    </div>
                `;

            const modalContainer = document.createElement('div');
            modalContainer.innerHTML = modalHtml;
            document.body.appendChild(modalContainer);

            const authModal = new bootstrap.Modal(document.getElementById('authModal'));
            authModal.show();

            document.getElementById('authModal').addEventListener('hidden.bs.modal', function () {
                modalContainer.remove();
            });
        }
    } else {
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

// ==================== ЗАГРУЗКА СТАНЦИЙ ====================
async function loadStations() {
    try {
        const response = await fetch('/api/trains/stations');
        if (response.ok) {
            stationsData = await response.json();
            console.log('Загружено станций:', stationsData.length);
        } else {
            throw new Error('Ошибка загрузки станций');
        }
    } catch (error) {
        console.error('Ошибка загрузки станций:', error);
        stationsData = [];
    }
}

// ==================== АВТОДОПОЛНЕНИЕ ====================
function initializeAutocomplete() {
    const departureInput = document.getElementById('railwayDeparture');
    const arrivalInput = document.getElementById('railwayArrival');
    const departureDropdown = document.getElementById('railwayDepartureDropdown');
    const arrivalDropdown = document.getElementById('railwayArrivalDropdown');

    setupAutocomplete(departureInput, departureDropdown);
    setupAutocomplete(arrivalInput, arrivalDropdown);
}

function setupAutocomplete(input, dropdown) {
    let searchTimeout;

    input.addEventListener('input', function (e) {
        const query = e.target.value.trim();

        clearTimeout(searchTimeout);

        if (query.length < 2) {
            dropdown.style.display = 'none';
            return;
        }

        searchTimeout = setTimeout(async () => {
            const stations = await searchStationsFromApi(query);
            showAutocompleteResults(stations, dropdown, input);
        }, 300);
    });

    input.addEventListener('focus', async function () {
        const query = this.value.trim();
        if (query.length >= 2) {
            const stations = await searchStationsFromApi(query);
            showAutocompleteResults(stations, dropdown, input);
        }
    });

    document.addEventListener('click', function (e) {
        if (!input.contains(e.target) && !dropdown.contains(e.target)) {
            dropdown.style.display = 'none';
        }
    });
}

async function searchStationsFromApi(query) {
    try {
        const response = await fetch(`/api/trains/stations/search?query=${encodeURIComponent(query)}`);
        if (response.ok) {
            return await response.json();
        }
    } catch (error) {
        console.error('Ошибка поиска станций:', error);
    }
    return [];
}

function showAutocompleteResults(stations, dropdown, input) {
    if (stations.length === 0) {
        dropdown.style.display = 'none';
        return;
    }

    dropdown.innerHTML = stations.map(station => `
            <div class="autocomplete-item" data-station='${JSON.stringify(station).replace(/'/g, "&apos;")}'>
                <div class="station-name">${station.name}</div>
                <div class="station-region">${station.region || ''}</div>
            </div>
        `).join('');

    dropdown.style.display = 'block';

    dropdown.querySelectorAll('.autocomplete-item').forEach(item => {
        item.addEventListener('click', function () {
            const station = JSON.parse(this.getAttribute('data-station').replace(/&apos;/g, "'"));
            input.value = station.name;
            input.setAttribute('data-station-id', station.id);
            dropdown.style.display = 'none';
        });
    });
}

// ==================== ДАТЫ ====================
function initializeDateInputs() {
    const today = new Date();
    const departureDateInput = document.getElementById('railwayDepartureDate');
    const returnDateInput = document.getElementById('railwayReturnDate');

    // Устанавливаем минимальные даты
    if (departureDateInput) {
        departureDateInput.min = today.toISOString().split('T')[0];
        // НЕ СТАВИМ ЗНАЧЕНИЕ - оставляем пустым
        departureDateInput.value = '';
    }

    if (returnDateInput) {
        const tomorrow = new Date(today);
        tomorrow.setDate(tomorrow.getDate() + 1);
        returnDateInput.min = tomorrow.toISOString().split('T')[0];
        // НЕ СТАВИМ ЗНАЧЕНИЕ - оставляем пустым
        returnDateInput.value = '';
    }

    if (departureDateInput && returnDateInput) {
        departureDateInput.addEventListener('change', function () {
            if (this.value) {
                returnDateInput.min = this.value;
                if (returnDateInput.value && returnDateInput.value < this.value) {
                    returnDateInput.value = this.value;
                }
            } else {
                const tomorrow = new Date(today);
                tomorrow.setDate(tomorrow.getDate() + 1);
                returnDateInput.min = tomorrow.toISOString().split('T')[0];
            }
        });
    }
}

// ==================== ПОИСК ====================
function setupFormHandler() {
    const form = document.getElementById('railwaySearchForm');
    const resultsContainer = document.getElementById('railwayResults');
    const loadingIndicator = document.getElementById('railwayLoading');

    form.addEventListener('submit', async function (e) {
        e.preventDefault();

        console.log('=== ОТПРАВКА ФОРМЫ ===');

        const departureStation = document.getElementById('railwayDeparture');
        const arrivalStation = document.getElementById('railwayArrival');
        const departureDate = document.getElementById('railwayDepartureDate');
        const returnDate = document.getElementById('railwayReturnDate');
        const passengers = document.getElementById('railwayPassengers');

        console.log('Departure station value:', departureStation.value);
        console.log('Departure station ID:', departureStation.getAttribute('data-station-id'));
        console.log('Arrival station value:', arrivalStation.value);
        console.log('Arrival station ID:', arrivalStation.getAttribute('data-station-id'));
        console.log('Departure date:', departureDate.value);
        console.log('Return date:', returnDate.value);
        console.log('Passengers:', passengers.value);

        if (!departureStation.getAttribute('data-station-id') ||
            !arrivalStation.getAttribute('data-station-id')) {
            showNotification('Пожалуйста, выберите станции из списка', 'warning');
            return;
        }

        if (departureStation.getAttribute('data-station-id') === arrivalStation.getAttribute('data-station-id')) {
            showNotification('Станции отправления и назначения не могут совпадать', 'warning');
            return;
        }

        // ========== ПРОВЕРКА, ЧТО ДАТА ВЫБРАНА ==========
        if (!departureDate.value) {
            showNotification('Пожалуйста, выберите дату отправления', 'warning');
            return;
        }

        // Для обратного рейса не проверяем - можно в одну сторону

        loadingIndicator.classList.remove('d-none');
        resultsContainer.innerHTML = '';

        try {
            const searchData = {
                departureStationId: departureStation.getAttribute('data-station-id'),
                arrivalStationId: arrivalStation.getAttribute('data-station-id'),
                departureDate: departureDate.value,
                returnDate: returnDate.value || null,
                passengers: parseInt(passengers.value)
            };

            const trainGroups = await searchTrains(searchData);
            displaySearchResults(trainGroups, searchData);

        } catch (error) {
            console.error('Ошибка поиска:', error);
            resultsContainer.innerHTML = `
                <div class="alert alert-danger">
                    <h5>Произошла ошибка при поиске</h5>
                    <p>${error.message || 'Пожалуйста, попробуйте позже'}</p>
                </div>
            `;
        } finally {
            loadingIndicator.classList.add('d-none');
        }
    });
}

// Замените функцию searchTrains на эту версию с улучшенным логированием
async function searchTrains(searchData) {
    console.log('Поиск поездов:', searchData);

    // Используем только mock данные для стабильной работы
    const useMockData = true; // Меняйте на false, если хотите использовать реальное API

    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 15000);

    try {
        const url = useMockData ? '/api/trains/search-mock' : '/api/trains/search';

        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(searchData),
            signal: controller.signal
        });

        clearTimeout(timeoutId);

        if (!response.ok) {
            throw new Error(`Ошибка сервера: ${response.status}`);
        }

        const result = await response.json();
        console.log('Ответ от API:', result);

        if (result.success) {
            const trainGroups = result.trainGroups || [];

            if (trainGroups.length === 0) {
                showNotification('Поезда не найдены. Попробуйте другие даты.', 'warning');
            }

            return trainGroups;
        } else {
            throw new Error(result.message || result.error || 'Ошибка при поиске поездов');
        }
    } catch (error) {
        clearTimeout(timeoutId);
        console.error('Ошибка поиска:', error);

        if (error.name === 'AbortError') {
            showNotification('Превышено время ожидания. Попробуйте позже.', 'danger');
        } else {
            showNotification(error.message || 'Ошибка при поиске поездов', 'danger');
        }
        return [];
    }
}
// ==================== ОТОБРАЖЕНИЕ РЕЗУЛЬТАТОВ ====================
function displaySearchResults(trainGroups, searchData) {
    const resultsContainer = document.getElementById('railwayResults');

    if (!trainGroups || trainGroups.length === 0) {
        resultsContainer.innerHTML = `
            <div class="alert alert-info">
                <h5>Поезда не найдены</h5>
                <p>Попробуйте изменить параметры поиска или даты</p>
            </div>
        `;
        return;
    }

    let html = `
        <div class="d-flex justify-content-between align-items-center mb-4">
            <h3>Найдено вариантов: ${trainGroups.length}</h3>
            <div class="text-muted">
                ${getStationNameById(searchData.departureStationId)} →
                ${getStationNameById(searchData.arrivalStationId)}
                ${searchData.returnDate ? ' (туда и обратно)' : ' (в одну сторону)'}
            </div>
        </div>
    `;

    trainGroups.forEach((group, index) => {
        html += generateTrainGroupCard(group, searchData, index);
    });

    resultsContainer.innerHTML = html;

    // Обновляем состояние кнопок избранного после отображения
    if (isUserAuthenticated) {
        setTimeout(() => {
            updateAllTrainFavoriteButtons();
        }, 100);
    }

    // Обновляем кнопки покупки (для неавторизованных)
    setTimeout(() => {
        updateAllBuyButtons();
    }, 150);
}

function generateTrainGroupCard(group, searchData, index) {
    console.log('=== ДАННЫЕ ПОЕЗДА ===');
    console.log('group.forwardTrain:', group.forwardTrain);
    console.log('departureDate:', group.forwardTrain.departureDate);
    console.log('arrivalDate:', group.forwardTrain.arrivalDate);
    console.log('departureTime:', group.forwardTrain.departureTime);
    console.log('arrivalTime:', group.forwardTrain.arrivalTime);
    const uniqueGroupId = `train-group-${group.id}-${index}`;
    const isRoundTrip = group.isRoundTrip || false;
    const typeColor = isRoundTrip ? 'secondary' : 'primary';
    const typeIcon = isRoundTrip ? 'fa-exchange-alt' : 'fa-train';
    const typeBadge = isRoundTrip ?
        '<span class="badge bg-secondary ms-2">Туда и обратно</span>' :
        '<span class="badge bg-primary ms-2">Только туда</span>';

    // Получаем названия станций
    const departureStationName = getStationNameById(group.forwardTrain.departureStation);
    const arrivalStationName = getStationNameById(group.forwardTrain.arrivalStation);

    // Проверяем авторизацию
    const isAuth = isUserAuthenticated;

    // Функция для форматирования даты и времени в ISO строку
    function formatToISO(dateStr, timeStr) {
        // Если дата не передана, используем дату из формы поиска
        if (!dateStr || !timeStr) {
            console.log('Отсутствует дата или время:', { dateStr, timeStr });
            // Пытаемся получить дату из формы
            const searchDate = document.getElementById('railwayDepartureDate')?.value;
            if (searchDate && timeStr) {
                console.log('Используем дату из формы:', searchDate);
                dateStr = searchDate;
            } else {
                const now = new Date();
                return now.toISOString();
            }
        }

        try {
            if (dateStr.includes('.')) {
                const parts = dateStr.split('.');
                if (parts.length === 3) {
                    const year = parts[2];
                    const month = parts[1].padStart(2, '0');
                    const day = parts[0].padStart(2, '0');
                    let time = timeStr;
                    if (time && !time.includes(':')) {
                        time = time.substring(0, 2) + ':' + time.substring(2, 4);
                    }
                    const isoString = `${year}-${month}-${day}T${time || '00:00'}:00`;
                    return isoString;
                }
            }
            if (dateStr.includes('-')) {
                const isoString = `${dateStr}T${timeStr || '00:00'}:00`;
                return isoString;
            }
            return new Date().toISOString();
        } catch (error) {
            console.error('Ошибка форматирования даты:', error, { dateStr, timeStr });
            return new Date().toISOString();
        }
    }

    const departureDateTime = formatToISO(group.forwardTrain.departureDate, group.forwardTrain.departureTime);
    const arrivalDateTime = formatToISO(group.forwardTrain.arrivalDate, group.forwardTrain.arrivalTime);

    let returnDepartureDateTime = null;
    let returnArrivalDateTime = null;

    if (group.returnTrain) {
        returnDepartureDateTime = formatToISO(group.returnTrain.departureDate, group.returnTrain.departureTime);
        returnArrivalDateTime = formatToISO(group.returnTrain.arrivalDate, group.returnTrain.arrivalTime);
    }

    // Получаем правильную дату из формы
    const searchDepartureDate = document.getElementById('railwayDepartureDate')?.value;

    // Функция для создания правильной даты (в локальном времени, без UTC смещения)
    // Функция для создания правильной даты (в локальном времени, без UTC смещения)
    function createCorrectDateTime(dateStr, timeStr) {
        if (!dateStr || !timeStr) return null;

        console.log('=== createCorrectDateTime ===');
        console.log('Входные данные:', { dateStr, timeStr });

        // Если dateStr в формате YYYY-MM-DD (из input)
        if (dateStr.includes('-')) {
            const [year, month, day] = dateStr.split('-');
            const [hours, minutes] = timeStr.split(':');
            console.log('Распарсено YYYY-MM-DD:', { year, month, day, hours, minutes });

            // СОЗДАЕМ ДАТУ В ЛОКАЛЬНОМ ВРЕМЕНИ
            const date = new Date(parseInt(year), parseInt(month) - 1, parseInt(day), parseInt(hours), parseInt(minutes));
            console.log('Созданная дата (локальная):', date.toString());
            console.log('ISO строка:', date.toISOString());

            return date.toISOString();
        }

        // Если dateStr в формате DD.MM.YYYY (из API)
        if (dateStr.includes('.')) {
            const [day, month, year] = dateStr.split('.');
            const [hours, minutes] = timeStr.split(':');
            console.log('Распарсено DD.MM.YYYY:', { year, month, day, hours, minutes });

            const date = new Date(parseInt(year), parseInt(month) - 1, parseInt(day), parseInt(hours), parseInt(minutes));
            console.log('Созданная дата (локальная):', date.toString());
            console.log('ISO строка:', date.toISOString());

            return date.toISOString();
        }

        return null;
    }

    // Создаем правильные даты на основе данных поиска
    let correctDepartureDateTime = null;
    let correctArrivalDateTime = null;

    if (searchDepartureDate && group.forwardTrain.departureTime) {
        correctDepartureDateTime = createCorrectDateTime(searchDepartureDate, group.forwardTrain.departureTime);

        if (group.forwardTrain.travelTime) {
            const durationMinutes = parseDuration(group.forwardTrain.travelTime);
            const departureDate = new Date(correctDepartureDateTime);
            const arrivalDate = new Date(departureDate.getTime() + durationMinutes * 60 * 1000);
            correctArrivalDateTime = arrivalDate.toISOString();
        }
    }

    let correctReturnDepartureDateTime = null;
    let correctReturnArrivalDateTime = null;

    if (isRoundTrip && group.returnTrain) {
        const searchReturnDate = document.getElementById('railwayReturnDate')?.value;
        if (searchReturnDate && group.returnTrain.departureTime) {
            correctReturnDepartureDateTime = createCorrectDateTime(searchReturnDate, group.returnTrain.departureTime);

            if (group.returnTrain.travelTime) {
                const returnDurationMinutes = parseDuration(group.returnTrain.travelTime);
                const returnDepartureDate = new Date(correctReturnDepartureDateTime);
                const returnArrivalDate = new Date(returnDepartureDate.getTime() + returnDurationMinutes * 60 * 1000);
                correctReturnArrivalDateTime = returnArrivalDate.toISOString();
            }
        }
    }
    console.log('=== ЦЕНА ПЕРЕД СОХРАНЕНИЕМ ===');
    console.log('group.totalPrice:', group.totalPrice);
    console.log('group.forwardTrain.categories:', group.forwardTrain?.categories);
    if (group.forwardTrain?.categories && group.forwardTrain.categories.length > 0) {
        console.log('Первая цена в категориях:', group.forwardTrain.categories[0].price);
    }
    // Подготовка данных для избранного с правильными датами
    const trainData = {
        trainGroupId: uniqueGroupId,
        forwardTrainNumber: group.forwardTrain.trainNumber,
        returnTrainNumber: group.returnTrain?.trainNumber || null,
        departureStation: departureStationName,
        arrivalStation: arrivalStationName,
        departureStationId: group.forwardTrain.departureStation,
        arrivalStationId: group.forwardTrain.arrivalStation,
        departureDateTime: correctDepartureDateTime || departureDateTime,
        returnDepartureDateTime: correctReturnDepartureDateTime || returnDepartureDateTime,
        arrivalDateTime: correctArrivalDateTime || arrivalDateTime,
        returnArrivalDateTime: correctReturnArrivalDateTime || returnArrivalDateTime,
        price: group.forwardTrain?.categories?.[0]?.price || group.totalPrice || 0,
        currency: "RUB",
        duration: parseDuration(group.forwardTrain.travelTime),
        returnDuration: group.returnTrain ? parseDuration(group.returnTrain.travelTime) : null,
        trainBrand: group.forwardTrain.brand || "",
        carrier: group.forwardTrain.carrier || "",
        isFirm: group.forwardTrain.firm || false,
        isRoundTrip: isRoundTrip,
        passengers: parseInt(document.getElementById('railwayPassengers').value) || 1,
        bookingUrl: window.location.href
    };

    // Кнопка Купить - разная для авторизованных и неавторизованных
    let buyButtonHtml = '';
    if (isAuth) {
        buyButtonHtml = `
            <button class="btn btn-success btn-buy-now"
                    onclick="bookTickets(${JSON.stringify(group).replace(/"/g, '&quot;')})">
                <i class="fas fa-shopping-cart me-2"></i>
                Купить билет
            </button>
        `;
    } else {
        buyButtonHtml = `
            <button class="btn btn-outline-success btn-buy-now"
                    onclick="showAuthModal()"
                    title="Войдите в аккаунт для покупки билетов">
                <i class="fas fa-lock me-2"></i>
                Войдите, чтобы купить
            </button>
        `;
    }

    // Генерируем HTML карточки
    let cardHtml = `
        <div class="card train-group-card shadow-sm mb-4 border-${typeColor}">
            <div class="card-header bg-${typeColor} text-white">
                <div class="d-flex justify-content-between align-items-center">
                    <div>
                        <i class="fas ${typeIcon} me-2"></i>
                        <strong>Вариант ${index + 1}</strong>
                        ${typeBadge}
                    </div>
                </div>
            </div>

            <div class="card-body">
                <!-- Секция "Поезд туда" -->
                ${generateTrainSection(group.forwardTrain, 'Туда', searchData.departureDate, 'primary')}

                ${isRoundTrip && group.returnTrain ?
            generateTrainSection(group.returnTrain, 'Обратно', searchData.returnDate, 'secondary') :
            ''}

                <div class="row mt-4">
                    <div class="col-md-8">
                        <h6>Стоимость билета:</h6>
                        ${generateCategoriesInfo(group)}
                    </div>
<div class="col-md-4 text-end">
    <div class="d-flex align-items-center justify-content-end gap-3">
        <button class="train-favorite-btn p-2 border-0 bg-transparent"
                data-train-group-id="${uniqueGroupId}"
                data-train-data='${JSON.stringify(trainData).replace(/'/g, "&apos;")}'
                onclick="handleTrainFavoriteClick(this)"
                title="${isAuth ? 'Добавить в избранное' : 'Войдите для сохранения'}"
                style="transition: transform 0.2s;">
            <i class="far fa-heart fa-lg text-muted"></i>
        </button>
        
        <div class="text-end">
            <div class="d-flex align-items-baseline justify-content-end">
                <h3 class="text-success mb-0">${(group.totalPrice || group.forwardTrain?.price || 0).toLocaleString('ru-RU')}</h3>
                <span class="text-success ms-1">₽</span>
            </div>
            <small class="text-muted d-block">за пассажира</small>
            ${buyButtonHtml}
        </div>
    </div>
</div>
                </div>
            </div>
        </div>
    `;

    return cardHtml;
}
// ==================== ОБНОВЛЕНИЕ КНОПОК ПОКУПКИ ====================
function updateAllBuyButtons() {
    const isAuth = isUserAuthenticated;

    const trainCards = document.querySelectorAll('.train-group-card');

    trainCards.forEach(card => {
        const buyButton = card.querySelector('.btn-buy-now');
        if (!buyButton) return;

        // Сохраняем данные группы (если есть)
        const onclickAttr = buyButton.getAttribute('onclick');

        if (isAuth) {
            // Если авторизован, меняем на кнопку "Купить"
            if (buyButton.textContent.includes('Войдите')) {
                // Нужно получить данные группы из атрибута
                const newBtn = document.createElement('button');
                newBtn.className = 'btn btn-success btn-buy-now';
                newBtn.innerHTML = '<i class="fas fa-shopping-cart me-2"></i>Купить билет';

                // Пытаемся восстановить данные из onclick
                if (onclickAttr && onclickAttr.includes('bookTickets')) {
                    const match = onclickAttr.match(/bookTickets\(({.*})\)/);
                    if (match && match[1]) {
                        const groupData = JSON.parse(match[1].replace(/&quot;/g, '"'));
                        newBtn.onclick = () => bookTickets(groupData);
                    } else {
                        newBtn.onclick = () => showNotification('Ошибка: данные не найдены', 'danger');
                    }
                } else {
                    newBtn.onclick = () => showNotification('Пожалуйста, обновите страницу', 'warning');
                }

                buyButton.replaceWith(newBtn);
            }
        } else {
            // Если не авторизован, меняем на кнопку "Войдите"
            if (buyButton.textContent.includes('Купить')) {
                const newBtn = document.createElement('button');
                newBtn.className = 'btn btn-outline-success btn-buy-now';
                newBtn.innerHTML = '<i class="fas fa-lock me-2"></i>Войдите, чтобы купить';
                newBtn.onclick = () => showAuthModal();
                buyButton.replaceWith(newBtn);
            }
        }
    });
}
function parseDuration(durationString) {
    if (!durationString) return 0;
    // Если длительность в формате "HH:MM"
    const parts = durationString.split(':');
    if (parts.length === 2) {
        return parseInt(parts[0]) * 60 + parseInt(parts[1]);
    }
    return 0;
}
// Функция для парсинга даты из формата RZD (DD.MM.YYYY)
function parseRzdDate(dateStr) {
    if (!dateStr) return null;
    try {
        const parts = dateStr.split('.');
        if (parts.length === 3) {
            return new Date(parts[2], parts[1] - 1, parts[0]);
        }
    } catch (error) {
        console.error('Ошибка парсинга даты:', error);
    }
    return null;
}

function generateTrainSection(train, title, date, color) {
    // Получаем названия станций
    const depStationName = getStationNameById(train.departureStation) || "Москва";
    const arrStationName = getStationNameById(train.arrivalStation) || "Санкт-Петербург";

    // Форматируем время
    const depTime = train.departureTime;
    const arrTime = train.arrivalTime;
    const durationText = train.travelTime || '—';

    // Форматируем дату
    let formattedDate = '';
    if (date) {
        try {
            const d = new Date(date);
            formattedDate = d.toLocaleDateString('ru-RU', { day: 'numeric', month: 'long' });
        } catch (e) {
            formattedDate = date;
        }
    }

    return `
        <div class="train-section mb-3 p-3 border-start border-3" style="border-left-color: #0379D9 !important;">
            <div class="d-flex justify-content-between align-items-center mb-3">
                <h6 class="mb-0" style="color: #0379D9;">
                    <i class="fas fa-train me-2"></i>
                    ${title}
                </h6>
                <div>
                    <span class="badge" style="background-color: #0379D9;">№ ${train.trainNumber}</span>
                    ${train.firm ? '<span class="badge bg-warning ms-1">Фирменный</span>' : ''}
                </div>
            </div>
            
            <div class="row align-items-center mb-3">
                <div class="col-12">
                    <div class="d-flex align-items-center">
                        <div class="time-badge departure-time">
                            <span class="fw-bold fs-4" style="color: #0379D9;">${depTime}</span>
                        </div>
                        <div class="ms-3">
                            <div class="fw-semibold fs-5">${depStationName}</div>
                            <div class="text-muted small">Отправление: ${formattedDate}</div>
                        </div>
                    </div>
                </div>
            </div>
            
            <div class="row align-items-center mb-3">
                <div class="col-12 text-center py-2">
                    <i class="fas fa-clock text-primary me-2"></i>
                    <span class="fw-semibold">В пути: ${durationText}</span>
                </div>
            </div>
            
            <div class="row align-items-center">
                <div class="col-12">
                    <div class="d-flex align-items-center">
                        <div class="time-badge arrival-time">
                            <span class="fw-bold fs-4" style="color: #0379D9;">${arrTime}</span>
                        </div>
                        <div class="ms-3">
                            <div class="fw-semibold fs-5">${arrStationName}</div>
                            <div class="text-muted small">Прибытие: ${formattedDate}</div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function generateCategoriesInfo(group) {
    let html = '';

    if (group.forwardTrain?.categories && group.forwardTrain.categories.length > 0) {
        html += '<div class="mb-2">';
        html += '<small class="text-muted d-block mb-1">🚆 Цены от:</small>';
        html += '<div class="d-flex flex-wrap gap-2">';

        // Показываем все категории с ценами
        group.forwardTrain.categories.slice(0, 3).forEach(cat => {
            const typeName = getCategoryName(cat.type);
            html += `
                <div class="bg-success bg-opacity-10 p-2 rounded">
                    <span class="badge bg-secondary me-1">${typeName}</span>
                    <strong style="color: #40B624;">${cat.price.toLocaleString('ru-RU')} ₽</strong>
                </div>
            `;
        });

        html += '</div>';
        html += '</div>';
    }

    if (group.isRoundTrip && group.returnTrain?.categories && group.returnTrain.categories.length > 0) {
        html += '<div class="mt-2">';
        html += '<small class="text-muted d-block mb-1">🔄 Обратно от:</small>';
        html += '<div class="d-flex flex-wrap gap-2">';

        group.returnTrain.categories.slice(0, 3).forEach(cat => {
            const typeName = getCategoryName(cat.type);
            html += `
                <div class="bg-success bg-opacity-10 p-2 rounded">
                    <span class="badge bg-secondary me-1">${typeName}</span>
                    <strong style="color: #40B624;">${cat.price.toLocaleString('ru-RU')} ₽</strong>
                </div>
            `;
        });

        html += '</div>';
        html += '</div>';
    }

    return html;
} 

// ==================== ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ====================
function formatDateForDisplay(dateString) {
    if (!dateString) return '';
    try {
        const date = new Date(dateString);
        if (isNaN(date.getTime())) return dateString;
        return date.toLocaleDateString('ru-RU', {
            weekday: 'short',
            day: 'numeric',
            month: 'long'
        });
    } catch (error) {
        return dateString;
    }
}

function formatTime(timeString) {
    if (!timeString) return '--:--';
    return timeString.substring(0, 5);
}

function formatDateTime(dateString, timeString) {
    if (!dateString) return '';
    try {
        if (dateString.includes('-')) {
            return `${dateString}T${timeString || '00:00'}`;
        }
        const parts = dateString.split('.');
        if (parts.length === 3) {
            return `${parts[2]}-${parts[1]}-${parts[0]}T${timeString || '00:00'}`;
        }
    } catch (error) {
        console.error('Ошибка форматирования даты:', error);
    }
    return '';
}

function parseDuration(durationString) {
    if (!durationString) return 0;
    const parts = durationString.split(':');
    if (parts.length === 2) {
        return parseInt(parts[0]) * 60 + parseInt(parts[1]);
    }
    return 0;
}

function getCategoryName(type) {
    const categories = {
        'plazcard': 'Плацкарт',
        'coupe': 'Купе',
        'sedentary': 'Сидячий',
        'lux': 'Люкс',
        'soft': 'Мягкий'
    };
    return categories[type] || type;
}

function getStationName(stationId) {
    if (!stationId) return stationId;
    // Используем новую функцию с маппингом
    return getStationNameById(stationId);
}

// ==================== ДЕЙСТВИЯ ====================
function selectRoundTrip(forwardTrainNumber, returnTrainNumber) {
    console.log('Выбран комбинированный вариант:', forwardTrainNumber, returnTrainNumber);
    showNotification(`Выбраны поезда: №${forwardTrainNumber} и №${returnTrainNumber}`, 'info');
}

function selectOneWay(trainNumber) {
    console.log('Выбран односторонний вариант:', trainNumber);
    showNotification(`Выбран поезд: №${trainNumber}`, 'info');
}

function viewDetails(groupId) {
    console.log('Просмотр подробностей группы:', groupId);
    // Здесь можно открыть модальное окно
}

function bookTickets(group) {
    console.log('=== НАЧАЛО БРОНИРОВАНИЯ ===');
    console.log('group:', group);

    if (!group.forwardTrain) {
        console.error('Ошибка: нет данных о поезде');
        showNotification('Ошибка: данные о поезде не найдены', 'danger');
        return;
    }

    // Получаем дату из формы поиска
    const departureDateInput = document.getElementById('railwayDepartureDate');
    let departureDateStr = departureDateInput?.value;

    if (!departureDateStr) {
        showNotification('Пожалуйста, выберите дату отправления', 'warning');
        return;
    }

    // ✅ ПРАВИЛЬНО ПОЛУЧАЕМ ЦЕНУ
    // Для туда-обратно - берем общую цену из group.totalPrice или группируем
    let forwardPrice = 0;
    let returnPrice = 0;
    let totalPrice = 0;

    // Получаем цену туда
    if (group.forwardTrain?.price) {
        forwardPrice = group.forwardTrain.price;
    } else if (group.forwardTrain?.categories && group.forwardTrain.categories.length > 0) {
        forwardPrice = group.forwardTrain.categories[0].price;
    }

    // Если есть обратный рейс
    if (group.isRoundTrip && group.returnTrain) {
        if (group.returnTrain?.price) {
            returnPrice = group.returnTrain.price;
        } else if (group.returnTrain?.categories && group.returnTrain.categories.length > 0) {
            returnPrice = group.returnTrain.categories[0].price;
        }
    }

    // ✅ ВАЖНО: totalPrice - это ЦЕНА ТУДА + ЦЕНА ОБРАТНО (за одного пассажира)
    if (group.isRoundTrip) {
        if (group.totalPrice && group.totalPrice > 0) {
            // Используем totalPrice из группы (если есть)
            totalPrice = group.totalPrice;
        } else if (forwardPrice > 0 && returnPrice > 0) {
            totalPrice = forwardPrice + returnPrice;
        } else if (forwardPrice > 0) {
            // Если обратная цена не определена, удваиваем цену туда
            totalPrice = forwardPrice * 2;
        }
    } else {
        totalPrice = forwardPrice;
    }

    console.log('=== ЦЕНЫ ===');
    console.log('forwardPrice:', forwardPrice);
    console.log('returnPrice:', returnPrice);
    console.log('totalPrice (за пассажира):', totalPrice);

    // Формируем дату и время отправления (локальное время)
    const departureTime = group.forwardTrain.departureTime || '00:00';
    const [depHours, depMinutes] = departureTime.split(':');
    const departureDateTime = new Date(
        parseInt(departureDateStr.split('-')[0]),
        parseInt(departureDateStr.split('-')[1]) - 1,
        parseInt(departureDateStr.split('-')[2]),
        parseInt(depHours),
        parseInt(depMinutes)
    );

    // Формируем дату и время прибытия
    let arrivalDateTime;
    if (group.forwardTrain.arrivalDate && group.forwardTrain.arrivalTime) {
        const [arrDay, arrMonth, arrYear] = group.forwardTrain.arrivalDate.split('.');
        const [arrHours, arrMinutes] = group.forwardTrain.arrivalTime.split(':');
        arrivalDateTime = new Date(
            parseInt(arrYear),
            parseInt(arrMonth) - 1,
            parseInt(arrDay),
            parseInt(arrHours),
            parseInt(arrMinutes)
        );
    } else {
        const duration = parseDuration(group.forwardTrain.travelTime);
        arrivalDateTime = new Date(departureDateTime.getTime() + duration * 60 * 1000);
    }

    // Форматируем для URL
    const formatForUrl = (date) => {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hours = String(date.getHours()).padStart(2, '0');
        const minutes = String(date.getMinutes()).padStart(2, '0');
        return `${year}-${month}-${day}T${hours}:${minutes}`;
    };

    // Получаем тип вагона
    let carType = 'coupe';
    let carClass = '2К';
    if (group.forwardTrain?.categories?.length > 0) {
        carType = group.forwardTrain.categories[0].type;
        const classMap = {
            'plazcard': '3П',
            'coupe': '2К',
            'sedentary': '2С',
            'lux': '1Л',
            'soft': '1М'
        };
        carClass = classMap[carType] || '2К';
    }

    // Получаем количество пассажиров
    const passengersCount = document.getElementById('railwayPassengers')?.value || '1';

    // Получаем названия станций
    const departureStationName = getStationNameById(group.forwardTrain.departureStation);
    const arrivalStationName = getStationNameById(group.forwardTrain.arrivalStation);

    // Длительность
    const duration = parseDuration(group.forwardTrain.travelTime || '0:00');

    // Формируем URL
    const params = new URLSearchParams();
    params.append('trainNumber', group.forwardTrain.trainNumber);
    params.append('departureStationId', group.forwardTrain.departureStation);
    params.append('departureStationName', departureStationName);
    params.append('arrivalStationId', group.forwardTrain.arrivalStation);
    params.append('arrivalStationName', arrivalStationName);
    params.append('departureDateTime', formatForUrl(departureDateTime));
    params.append('arrivalDateTime', formatForUrl(arrivalDateTime));
    // ✅ ИСПРАВЛЕНО: передаем totalPrice (цена туда+обратно за одного пассажира)
    params.append('price', totalPrice.toString());
    params.append('passengers', passengersCount);
    params.append('carType', carType);
    params.append('carClass', carClass);
    params.append('duration', duration.toString());
    params.append('isRoundTrip', (group.isRoundTrip || false).toString());

    if (group.isRoundTrip && group.returnTrain) {
        const returnDateInput = document.getElementById('railwayReturnDate');
        let returnDateStr = returnDateInput?.value;

        if (returnDateStr) {
            const returnTime = group.returnTrain.departureTime || '00:00';
            const [retHours, retMinutes] = returnTime.split(':');
            const returnDepartureDateTime = new Date(
                parseInt(returnDateStr.split('-')[0]),
                parseInt(returnDateStr.split('-')[1]) - 1,
                parseInt(returnDateStr.split('-')[2]),
                parseInt(retHours),
                parseInt(retMinutes)
            );

            // Рассчитываем дату прибытия обратного рейса
            let returnArrivalDateTime;
            if (group.returnTrain.arrivalDate && group.returnTrain.arrivalTime) {
                const [arrDay, arrMonth, arrYear] = group.returnTrain.arrivalDate.split('.');
                const [arrHours, arrMinutes] = group.returnTrain.arrivalTime.split(':');
                returnArrivalDateTime = new Date(
                    parseInt(arrYear),
                    parseInt(arrMonth) - 1,
                    parseInt(arrDay),
                    parseInt(arrHours),
                    parseInt(arrMinutes)
                );
            } else {
                const returnDuration = parseDuration(group.returnTrain.travelTime);
                returnArrivalDateTime = new Date(returnDepartureDateTime.getTime() + returnDuration * 60 * 1000);
            }

            const returnDuration = parseDuration(group.returnTrain.travelTime || '0:00');

            params.append('returnTrainNumber', group.returnTrain.trainNumber);
            params.append('returnDepartureDateTime', formatForUrl(returnDepartureDateTime));
            params.append('returnArrivalDateTime', formatForUrl(returnArrivalDateTime));
            params.append('returnDuration', returnDuration.toString());
        } else {
            showNotification('Пожалуйста, выберите дату обратного отправления', 'warning');
            return;
        }
    }

    const url = '/TrainBooking/Book?' + params.toString();
    console.log('=== ПЕРЕХОД ПО URL ===');
    console.log('url:', url);
    console.log('Цена (за пассажира):', totalPrice);
    console.log('Количество пассажиров:', passengersCount);
    console.log('Общая сумма:', totalPrice * parseInt(passengersCount));

    window.location.replace(url);
}

function parseDuration(durationString) {
    if (!durationString) return 0;
    const parts = durationString.split(':');
    if (parts.length === 2) {
        return parseInt(parts[0]) * 60 + parseInt(parts[1]);
    }
    return 0;
}

// ==================== ПОПУЛЯРНЫЕ НАПРАВЛЕНИЯ ====================
function loadPopularRailwayDestinations() {
    try {
        setTimeout(() => {
            const popularDestinationsHTML = `
                <div class="row g-4">
                    <div class="col-md-4">
                        <div class="card destination-card h-100" onclick="selectRailwayDestination('Москва', 'Санкт-Петербург')">
                            <img src="https://avatars.mds.yandex.net/get-weather/5278294/WXQuWFvnoHzOTdGjRRZU/orig" class="card-img-top" alt="Москва - Санкт-Петербург">
                            <div class="card-body">
                                <h5 class="card-title">Москва → Санкт-Петербург</h5>
                                <p class="card-text">От 1 800 ₽</p>
                                <p class="text-muted small">В пути от 4 часов</p>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="card destination-card h-100" onclick="selectRailwayDestination('Москва', 'Нижний Новгород')">
                            <img src="https://avatars.dzeninfra.ru/get-zen_doc/8302711/pub_6466413b3c24f951449f3665_646643977733f348e0d5d55d/scale_1200" class="card-img-top" alt="Москва - Нижний Новгород">
                            <div class="card-body">
                                <h5 class="card-title">Москва → Нижний Новгород</h5>
                                <p class="card-text">От 1 500 ₽</p>
                                <p class="text-muted small">В пути от 4 часов</p>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="card destination-card h-100" onclick="selectRailwayDestination('Москва', 'Казань')">
                            <img src="https://cdn.culture.ru/images/a95b2c46-77db-5224-a88b-1079b9f3c3b0" class="card-img-top" alt="Москва - Казань">
                            <div class="card-body">
                                <h5 class="card-title">Москва → Казань</h5>
                                <p class="card-text">От 2 200 ₽</p>
                                <p class="text-muted small">В пути от 12 часов</p>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="row g-4 mt-2">
                    <div class="col-md-4">
                        <div class="card destination-card h-100" onclick="selectRailwayDestination('Москва', 'Екатеринбург')">
                            <img src="https://photobooth.cdn.sports.ru/preset/post/7/0e/3974000c34c258259e1a56a9ab84b.jpeg?f=webp&q=90&s=2x&w=730" class="card-img-top" alt="Москва - Екатеринбург">
                            <div class="card-body">
                                <h5 class="card-title">Москва → Екатеринбург</h5>
                                <p class="card-text">От 2 800 ₽</p>
                                <p class="text-muted small">В пути от 1 дня 2 часов</p>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="card destination-card h-100" onclick="selectRailwayDestination('Москва', 'Краснодар')">
                            <img src="https://blog.ostrovok.ru/wp-content/uploads/2022/05/8-1.jpg" class="card-img-top" alt="Москва - Краснодар">
                            <div class="card-body">
                                <h5 class="card-title">Москва → Краснодар</h5>
                                <p class="card-text">От 2 500 ₽</p>
                                <p class="text-muted small">В пути от 1 дня 4 часов</p>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="card destination-card h-100" onclick="selectRailwayDestination('Москва', 'Сочи')">
                            <img src="https://upload.wikimedia.org/wikipedia/commons/thumb/2/28/Sochi_Park_Night.jpg/960px-Sochi_Park_Night.jpg" class="card-img-top" alt="Москва - Сочи">
                            <div class="card-body">
                                <h5 class="card-title">Москва → Сочи</h5>
                                <p class="card-text">От 3 200 ₽</p>
                                <p class="text-muted small">В пути от 1 дня 8 часов</p>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="row g-4 mt-2">
                    <div class="col-md-4">
                        <div class="card destination-card h-100" onclick="selectRailwayDestination('Москва', 'Владивосток')">
                            <img src="https://visit-primorye.ru/uploads/Mosty_Vladivostoka_giganty_stavshie_glavnymi_dostoprimechatelnostyami_goroda_183_464ad2ec74.jpg" class="card-img-top" alt="Москва - Владивосток">
                            <div class="card-body">
                                <h5 class="card-title">Москва → Владивосток</h5>
                                <p class="card-text">От 8 500 ₽</p>
                                <p class="text-muted small">В пути от 6 дней</p>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="card destination-card h-100" onclick="selectRailwayDestination('Москва', 'Новосибирск')">
                            <img src="https://avatars.mds.yandex.net/i?id=3c8e069f6919ddc94fd06875b3bb9958_l-10754966-images-thumbs&n=13" class="card-img-top" alt="Москва - Новосибирск">
                            <div class="card-body">
                                <h5 class="card-title">Москва → Новосибирск</h5>
                                <p class="card-text">От 4 500 ₽</p>
                                <p class="text-muted small">В пути от 2 дней 5 часов</p>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="card destination-card h-100" onclick="selectRailwayDestination('Москва', 'Калининград')">
                            <img src="https://fs.tonkosti.ru/sized/c960x400/7y/4s/7y4s5zdc88oww4sskoswcg4co.jpg" class="card-img-top" alt="Москва - Калининград">
                            <div class="card-body">
                                <h5 class="card-title">Москва → Калининград</h5>
                                <p class="card-text">От 5 000 ₽</p>
                                <p class="text-muted small">В пути от 1 дня 1 часа</p>
                            </div>
                        </div>
                    </div>
                </div>
            `;

            const container = document.getElementById('popularRailwayDestinations');
            if (container) container.innerHTML = popularDestinationsHTML;
        }, 1000);
    } catch (error) {
        console.error('Ошибка загрузки популярных направлений:', error);
    }
}

function selectRailwayDestination(fromCity, toCity) {
    console.log(`Выбрано ЖД направление: ${fromCity} → ${toCity}`);

    const fromId = getRailwayStationId(fromCity);
    const toId = getRailwayStationId(toCity);

    console.log(`ID станции отправления: ${fromId} (${fromCity})`);
    console.log(`ID станции прибытия: ${toId} (${toCity})`);

    // Проверка корректности ID
    if (toId === "2038000" && toCity !== "Красноярск" && toCity !== "Новосибирск") {
        console.warn(`ВНИМАНИЕ: Город "${toCity}" имеет ID "${toId}", который также используется для Красноярска/Новосибирска!`);
    }

    // Устанавливаем города
    document.getElementById('railwayDeparture').value = fromCity;
    document.getElementById('railwayArrival').value = toCity;
    document.getElementById('railwayDeparture').setAttribute('data-station-id', fromId);
    document.getElementById('railwayArrival').setAttribute('data-station-id', toId);

    // Прокручиваем к форме
    document.getElementById('railwaySearchForm').scrollIntoView({ behavior: 'smooth' });
    showNotification(`Направление ${fromCity} → ${toCity} добавлено в форму поиска. Выберите даты.`, 'success');
}
// ==================== МАППИНГ ID СТАНЦИЙ В НАЗВАНИЯ ====================
function getStationNameById(stationId) {
    if (!stationId) return stationId;

    // Словарь соответствия ID -> Название города
    const stationNameMap = {
        "2000000": "Москва",
        "2004000": "Санкт-Петербург",
        "2006000": "Санкт-Петербург",
        "2060000": "Нижний Новгород",
        "2060001": "Нижний Новгород",
        "2060501": "Казань",
        "2064000": "Ростов-на-Дону",
        "2064001": "Ростов-на-Дону",
        "2024000": "Самара",
        "2044000": "Екатеринбург",
        "2038000": "Новосибирск",
        "2064130": "Сочи",
        "2064788": "Краснодар",
        "2064110": "Новороссийск",
        "2064188": "Анапа",
        "2078001": "Симферополь",
        "2064150": "Адлер",
        "2060151": "Владивосток",
        "2060150": "Хабаровск",
        "2054000": "Иркутск",
        "2047000": "Тюмень",
        "2064050": "Волгоград",
        "2060002": "Калининград",
        "2024460": "Уфа",
        "2030000": "Красноярск",
        "2014000": "Воронеж"
    };

    // Сначала ищем в загруженных данных станций
    if (stationsData && stationsData.length > 0) {
        const station = stationsData.find(s => s.id === stationId);
        if (station && station.name) {
            return station.name;
        }
    }

    // Затем ищем в словаре
    if (stationNameMap[stationId]) {
        return stationNameMap[stationId];
    }

    // Если ничего не найдено, возвращаем ID
    return stationId;
}
function getRailwayStationId(cityName) {
    const stationIds = {
        "Москва": "2000000",
        "Санкт-Петербург": "2006000",
        "Нижний Новгород": "2060001",
        "Казань": "2060501",
        "Екатеринбург": "2044000",      // Правильный ID
        "Красноярск": "2038000",         // ID Красноярска
        "Новосибирск": "2038000",
        "Сочи": "2064130",
        "Краснодар": "2064788",
        "Ростов-на-Дону": "2064001",
        "Самара": "2024000",
        "Уфа": "2024460",
        "Владивосток": "2060151",
        "Калининград": "2060002",
        "Воронеж": "2014000",
        "Тюмень": "2047000",
        "Иркутск": "2054000",
        "Хабаровск": "2060150"
    };
    return stationIds[cityName] || "2000000";
}

// ==================== ИНИЦИАЛИЗАЦИЯ ====================
document.addEventListener('DOMContentLoaded', async function () {
    console.log('Инициализация страницы ЖД билетов...');
    await checkAuthStatus();
    await loadStations();
    initializeAutocomplete();
    initializeDateInputs();
    setupFormHandler();
    loadPopularRailwayDestinations();

    console.log('Инициализация завершена');
});