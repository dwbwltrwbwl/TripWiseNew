// friends.js - Полная система друзей с заявками

console.log('Friends script loaded');

// Глобальные переменные
let friendsList = [];
let friendRequests = [];

// ==================== ЗАГРУЗКА ДАННЫХ ====================

// Загрузка списка друзей
async function loadFriends() {
    try {
        const response = await fetch('/api/friends/GetFriends');
        const result = await response.json();

        if (result.success) {
            friendsList = result.data || [];
            displayFriends(friendsList);
            console.log('Загружено друзей:', friendsList.length);
        } else {
            showNotification(result.message || 'Ошибка загрузки друзей', 'danger');
        }
    } catch (error) {
        console.error('Ошибка при загрузке друзей:', error);
        showNotification('Не удалось загрузить список друзей', 'danger');
    }
}

// Загрузка запросов в друзья
async function loadFriendRequests() {
    try {
        const response = await fetch('/api/friends/GetFriendRequests');
        const result = await response.json();

        if (result.success) {
            friendRequests = result.data || [];
            displayFriendRequests(friendRequests);
            updateFriendRequestsBadge();
            console.log('Загружено запросов:', friendRequests.length);
        } else {
            showNotification(result.message || 'Ошибка загрузки запросов', 'danger');
        }
    } catch (error) {
        console.error('Ошибка при загрузке запросов:', error);
        showNotification('Не удалось загрузить запросы в друзья', 'danger');
    }
}

// ==================== ОТОБРАЖЕНИЕ ====================

// Отображение друзей
function displayFriends(friends) {
    const container = document.getElementById('friendsList');
    if (!container) return;

    if (friends.length === 0) {
        container.innerHTML = `
            <div class="text-center text-muted p-4">
                <i class="fas fa-user-friends fa-3x mb-3 opacity-50"></i>
                <p>У вас пока нет друзей</p>
                <p class="small">Найдите пользователей через поиск и отправьте заявку</p>
            </div>
        `;
        return;
    }

    let html = '';
    friends.forEach(friend => {
        html += `
            <div class="friend-item p-3 border-bottom d-flex justify-content-between align-items-center" data-friend-id="${friend.friendId}">
                <div class="d-flex align-items-center">
                    <div class="friend-avatar me-3">
                        ${friend.avatarPath ?
                `<img src="${friend.avatarPath}" class="rounded-circle" width="48" height="48" style="object-fit: cover;">` :
                `<div class="avatar-placeholder rounded-circle bg-primary text-white d-flex align-items-center justify-content-center" 
                                  style="width:48px;height:48px; font-size: 1.2rem; font-weight: 600;">
                            ${friend.firstName ? friend.firstName[0] : ''}${friend.lastName ? friend.lastName[0] : ''}
                        </div>`
            }
                    </div>
                    <div>
                        <div class="fw-bold">${escapeHtml(friend.fullName)}</div>
                        <small class="text-muted">${escapeHtml(friend.email)}</small>
                        ${friend.acceptedAt ?
                `<div class="small text-success mt-1">
                                <i class="fas fa-check-circle me-1"></i>Друг с ${new Date(friend.acceptedAt).toLocaleDateString()}
                            </div>` : ''
            }
                    </div>
                </div>
                <div class="d-flex gap-2">
                    <button class="btn btn-sm btn-outline-primary" onclick="startPrivateChat(${friend.friendId})" 
                            title="Написать сообщение">
                        <i class="fas fa-comment"></i>
                    </button>
                    <button class="btn btn-sm btn-outline-danger" onclick="removeFriend(${friend.friendId})" 
                            title="Удалить из друзей">
                        <i class="fas fa-user-minus"></i>
                    </button>
                </div>
            </div>
        `;
    });

    container.innerHTML = html;
}

// Отображение запросов в друзья
function displayFriendRequests(requests) {
    const container = document.getElementById('friendRequests');
    if (!container) return;

    if (requests.length === 0) {
        container.innerHTML = `
            <div class="text-center text-muted p-4">
                <i class="fas fa-user-clock fa-3x mb-3 opacity-50"></i>
                <p>Нет новых запросов в друзья</p>
            </div>
        `;
        return;
    }

    let html = '';
    requests.forEach(request => {
        html += `
            <div class="request-item p-3 border-bottom" data-request-id="${request.id}">
                <div class="d-flex justify-content-between align-items-center">
                    <div class="d-flex align-items-center">
                        <div class="me-3">
                            ${request.senderAvatar ?
                `<img src="${request.senderAvatar}" class="rounded-circle" width="48" height="48" style="object-fit: cover;">` :
                `<div class="avatar-placeholder rounded-circle bg-primary text-white d-flex align-items-center justify-content-center" 
                                  style="width:48px;height:48px; font-size: 1.2rem; font-weight: 600;">
                                    ${request.senderName ? request.senderName[0] : '?'}
                                </div>`
            }
                        </div>
                        <div>
                            <div class="fw-bold">${escapeHtml(request.senderName)}</div>
                            <small class="text-muted">
                                <i class="far fa-clock me-1"></i>${new Date(request.sentAt).toLocaleString()}
                            </small>
                            ${request.message ? `<div class="small text-muted mt-1">"${escapeHtml(request.message)}"</div>` : ''}
                        </div>
                    </div>
                    <div class="d-flex gap-2">
                        <button class="btn btn-sm btn-success" onclick="acceptFriendRequest(${request.id})" 
                                title="Принять заявку">
                            <i class="fas fa-check"></i>
                        </button>
                        <button class="btn btn-sm btn-danger" onclick="rejectFriendRequest(${request.id})" 
                                title="Отклонить заявку">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                </div>
            </div>
        `;
    });

    container.innerHTML = html;
}

// Обновление бейджа с количеством запросов
function updateFriendRequestsBadge() {
    const badge = document.getElementById('friendRequestsBadge');
    if (badge) {
        if (friendRequests.length > 0) {
            badge.textContent = friendRequests.length;
            badge.style.display = 'inline';
        } else {
            badge.style.display = 'none';
        }
    }
}

// ==================== ПОИСК ПОЛЬЗОВАТЕЛЕЙ ====================

// Поиск пользователей с отображением статуса дружбы
async function searchUsers(term) {
    try {
        const response = await fetch(`/api/friends/SearchUsers?term=${encodeURIComponent(term)}`);
        const result = await response.json();

        if (result.success) {
            displaySearchResults(result.data || []);
        } else {
            showNotification(result.message || 'Ошибка поиска', 'danger');
        }
    } catch (error) {
        console.error('Ошибка при поиске:', error);
        showNotification('Не удалось выполнить поиск', 'danger');
    }
}

// Отображение результатов поиска с кнопками действий
function displaySearchResults(users) {
    const container = document.getElementById('searchResults');
    if (!container) return;

    if (users.length === 0) {
        container.innerHTML = `
            <div class="text-center text-muted p-4">
                <i class="fas fa-user-slash fa-3x mb-3 opacity-50"></i>
                <p>Ничего не найдено</p>
            </div>
        `;
        return;
    }

    let html = '';
    users.forEach(user => {
        if (user.id !== currentUserId) {
            let actionButton = '';
            let actionClass = '';

            if (user.isFriend) {
                actionButton = `
                    <button class="btn btn-sm btn-success" disabled>
                        <i class="fas fa-check me-1"></i>Друг
                    </button>
                `;
                actionClass = 'friend-status-accepted';
            } else if (user.friendStatus === 'pending_sent') {
                actionButton = `
                    <button class="btn btn-sm btn-secondary" disabled>
                        <i class="fas fa-clock me-1"></i>Заявка отправлена
                    </button>
                `;
                actionClass = 'friend-status-pending-sent';
            } else if (user.friendStatus === 'pending_received') {
                actionButton = `
                    <div class="d-flex gap-1">
                        <button class="btn btn-sm btn-success" onclick="acceptFriendRequestFromSearch(${user.id}, event)">
                            <i class="fas fa-check"></i>
                        </button>
                        <button class="btn btn-sm btn-danger" onclick="rejectFriendRequestFromSearch(${user.id}, event)">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                `;
                actionClass = 'friend-status-pending-received';
            } else {
                actionButton = `
                    <button class="btn btn-sm btn-primary" onclick="sendFriendRequest(${user.id}, event)">
                        <i class="fas fa-user-plus me-1"></i>Добавить
                    </button>
                `;
                actionClass = 'friend-status-none';
            }

            html += `
                <div class="search-result-item p-3 border-bottom d-flex justify-content-between align-items-center ${actionClass}" 
                     data-user-id="${user.id}">
                    <div class="d-flex align-items-center">
                        <div class="me-3">
                            ${user.avatarPath ?
                    `<img src="${user.avatarPath}" class="rounded-circle" width="48" height="48" style="object-fit: cover;">` :
                    `<div class="avatar-placeholder rounded-circle bg-primary text-white d-flex align-items-center justify-content-center" 
                                  style="width:48px;height:48px; font-size: 1.2rem; font-weight: 600;">
                                    ${user.firstName ? user.firstName[0] : ''}${user.lastName ? user.lastName[0] : ''}
                                </div>`
                }
                        </div>
                        <div>
                            <div class="fw-bold">${escapeHtml(user.fullName)}</div>
                            <small class="text-muted">${escapeHtml(user.email)}</small>
                        </div>
                    </div>
                    <div class="friend-action-buttons">
                        ${actionButton}
                    </div>
                </div>
            `;
        }
    });

    container.innerHTML = html;
}

// ==================== ОТПРАВКА ЗАЯВОК ====================

// Отправка заявки в друзья
async function sendFriendRequest(userId, event) {
    if (event) event.stopPropagation();

    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const response = await fetch('/api/friends/SendFriendRequest', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(userId)
        });

        const result = await response.json();

        if (result.success) {
            showNotification('Заявка в друзья отправлена!', 'success');

            // Обновляем кнопку
            const userElement = document.querySelector(`[data-user-id="${userId}"] .friend-action-buttons`);
            if (userElement) {
                userElement.innerHTML = `
                    <button class="btn btn-sm btn-secondary" disabled>
                        <i class="fas fa-clock me-1"></i>Заявка отправлена
                    </button>
                `;
            }
        } else {
            showNotification(result.message || 'Ошибка при отправке заявки', 'danger');
        }
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Не удалось отправить заявку', 'danger');
    }
}

// ==================== ПРИНЯТИЕ/ОТКЛОНЕНИЕ ЗАЯВОК ====================

// Принять заявку в друзья
async function acceptFriendRequest(requestId) {
    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const response = await fetch('/api/friends/AcceptFriendRequest', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(requestId)
        });

        const result = await response.json();

        if (result.success) {
            showNotification('Друг добавлен!', 'success');

            // Удаляем заявку из списка
            const requestElement = document.querySelector(`[data-request-id="${requestId}"]`);
            if (requestElement) {
                requestElement.remove();
            }

            // Перезагружаем списки
            await loadFriendRequests();
            await loadFriends();

            // Обновляем результаты поиска если есть активный поиск
            const searchInput = document.getElementById('searchUsersInput');
            if (searchInput && searchInput.value.length >= 2) {
                searchUsers(searchInput.value);
            }
        } else {
            showNotification(result.message || 'Ошибка при принятии заявки', 'danger');
        }
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Не удалось принять заявку', 'danger');
    }
}

// Отклонить заявку в друзья
async function rejectFriendRequest(requestId) {
    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const response = await fetch('/api/friends/RejectFriendRequest', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(requestId)
        });

        const result = await response.json();

        if (result.success) {
            showNotification('Заявка отклонена', 'info');

            // Удаляем заявку из списка
            const requestElement = document.querySelector(`[data-request-id="${requestId}"]`);
            if (requestElement) {
                requestElement.remove();
            }

            // Перезагружаем запросы
            await loadFriendRequests();

            // Обновляем результаты поиска если есть активный поиск
            const searchInput = document.getElementById('searchUsersInput');
            if (searchInput && searchInput.value.length >= 2) {
                searchUsers(searchInput.value);
            }
        } else {
            showNotification(result.message || 'Ошибка при отклонении заявки', 'danger');
        }
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Не удалось отклонить заявку', 'danger');
    }
}

// Принять заявку из результатов поиска
async function acceptFriendRequestFromSearch(userId, event) {
    event.stopPropagation();

    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const response = await fetch('/api/friends/AcceptFriendRequest', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(userId)
        });

        const result = await response.json();

        if (result.success) {
            showNotification('Друг добавлен!', 'success');

            // Обновляем кнопку
            const userElement = document.querySelector(`[data-user-id="${userId}"] .friend-action-buttons`);
            if (userElement) {
                userElement.innerHTML = `
                    <button class="btn btn-sm btn-success" disabled>
                        <i class="fas fa-check me-1"></i>Друг
                    </button>
                `;
            }
        } else {
            showNotification(result.message || 'Ошибка при принятии заявки', 'danger');
        }
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Не удалось принять заявку', 'danger');
    }
}

// Отклонить заявку из результатов поиска
async function rejectFriendRequestFromSearch(userId, event) {
    event.stopPropagation();

    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const response = await fetch('/api/friends/RejectFriendRequest', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(userId)
        });

        const result = await response.json();

        if (result.success) {
            showNotification('Заявка отклонена', 'info');

            // Обновляем кнопку
            const userElement = document.querySelector(`[data-user-id="${userId}"] .friend-action-buttons`);
            if (userElement) {
                userElement.innerHTML = `
                    <button class="btn btn-sm btn-primary" onclick="sendFriendRequest(${userId}, event)">
                        <i class="fas fa-user-plus me-1"></i>Добавить
                    </button>
                `;
            }
        } else {
            showNotification(result.message || 'Ошибка при отклонении заявки', 'danger');
        }
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Не удалось отклонить заявку', 'danger');
    }
}

// ==================== УДАЛЕНИЕ ИЗ ДРУЗЕЙ ====================

// Удалить друга
async function removeFriend(friendId) {
    if (!confirm('Вы уверены, что хотите удалить этого пользователя из друзей?')) return;

    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const response = await fetch('/api/friends/RemoveFriend', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(friendId)
        });

        const result = await response.json();

        if (result.success) {
            showNotification('Друг удален', 'info');

            // Перезагружаем списки
            await loadFriends();

            // Обновляем результаты поиска если есть активный поиск
            const searchInput = document.getElementById('searchUsersInput');
            if (searchInput && searchInput.value.length >= 2) {
                searchUsers(searchInput.value);
            }
        } else {
            showNotification(result.message || 'Ошибка при удалении друга', 'danger');
        }
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Не удалось удалить друга', 'danger');
    }
}

// Загрузка информации о чате
async function loadChatInfo(chatId) {
    try {
        // Добавьте проверку в самом начале
        if (!chatId || chatId === undefined || chatId === null) {
            console.error('loadChatInfo: chatId не передан или равен null/undefined');
            return { success: false, message: 'ID чата не указан' };
        }

        console.log('Загрузка информации о чате:', chatId, 'тип:', typeof chatId);

        const response = await fetch(`/Chats/GetChatInfo/${chatId}`);
        if (!response.ok) {
            throw new Error(`HTTP ошибка: ${response.status}`);
        }

        const data = await response.json();
        console.log('Информация о чате:', data);

        if (data.success && data.data) {
            document.getElementById('chatTitle').textContent = data.data.name || 'Чат';

            const memberNames = data.data.members ? data.data.members.map(m => m.fullName).join(', ') : '';
            document.getElementById('chatMembers').textContent = memberNames;

            const avatarIcon = document.querySelector('#chatAvatar i');
            if (avatarIcon) {
                avatarIcon.className = `fas ${getChatIcon(data.data.type)}`;
            }
            return { success: true, data: data.data };
        } else {
            console.error('Ошибка загрузки информации о чате:', data.message);
            return { success: false, message: data.message };
        }
    } catch (error) {
        console.error('Ошибка загрузки информации о чате:', error);
        return { success: false, message: error.message };
    }
}

// ==================== ЧАТЫ С ДРУЗЬЯМИ ====================
// Начать личный чат с другом
async function startPrivateChat(friendId) {
    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        console.log('Отправка запроса на создание чата с другом:', friendId);

        const response = await fetch('/Chats/CreatePrivateChat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(friendId)
        });

        if (!response.ok) {
            const errorText = await response.text();
            console.error('Ошибка ответа:', response.status, errorText);
            throw new Error(`HTTP ошибка: ${response.status}`);
        }

        const result = await response.json();
        console.log('Результат создания чата:', result);

        if (result.success) {
            showNotification('Чат открыт', 'success');

            // Получаем ID созданного чата
            const chatId = result.data;

            // Перенаправляем на страницу чатов с ID созданного чата
            window.location.href = `/Home/Chats?chatId=${chatId}`;
        } else {
            showNotification(result.message || 'Ошибка при создании чата', 'danger');
        }
    } catch (error) {
        console.error('Ошибка:', error);
        showNotification('Не удалось создать чат: ' + error.message, 'danger');
    }
}

// ==================== ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ====================

// Экранирование HTML
function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Показать уведомление
function showNotification(message, type = 'info') {
    // Удаляем старые уведомления
    const oldNotifications = document.querySelectorAll('.friends-notification');
    oldNotifications.forEach(notification => notification.remove());

    const notification = document.createElement('div');
    notification.className = `friends-notification alert alert-${type} alert-dismissible fade show position-fixed`;
    notification.style.cssText = `
        top: 20px;
        right: 20px;
        z-index: 9999;
        min-width: 300px;
        max-width: 400px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
    `;

    notification.innerHTML = `
        <div class="d-flex align-items-center">
            <i class="fas ${type === 'success' ? 'fa-check-circle' :
            type === 'danger' ? 'fa-exclamation-circle' :
                'fa-info-circle'} me-2 fa-lg"></i>
            <div class="flex-grow-1">${message}</div>
            <button type="button" class="btn-close ms-2" data-bs-dismiss="alert"></button>
        </div>
    `;

    document.body.appendChild(notification);

    // Автоматически скрываем через 3 секунды
    setTimeout(() => {
        if (notification.parentNode) {
            notification.classList.remove('show');
            setTimeout(() => notification.remove(), 150);
        }
    }, 3000);
}

// ==================== ИНИЦИАЛИЗАЦИЯ ====================

document.addEventListener('DOMContentLoaded', function () {
    // Загружаем друзей и запросы
    loadFriends();
    loadFriendRequests();

    // Обработчик поиска
    const searchInput = document.getElementById('searchUsersInput');
    if (searchInput) {
        let searchTimeout;
        searchInput.addEventListener('input', function () {
            clearTimeout(searchTimeout);
            const term = this.value.trim();

            if (term.length >= 2) {
                searchTimeout = setTimeout(() => searchUsers(term), 300);
            } else {
                document.getElementById('searchResults').innerHTML = `
                    <div class="text-center text-muted p-4">
                        <i class="fas fa-search fa-3x mb-3 opacity-50"></i>
                        <p>Введите минимум 2 символа для поиска</p>
                    </div>
                `;
            }
        });

        // Очистка по Escape
        searchInput.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                this.value = '';
                document.getElementById('searchResults').innerHTML = `
                    <div class="text-center text-muted p-4">
                        <i class="fas fa-search fa-3x mb-3 opacity-50"></i>
                        <p>Введите имя или email для поиска</p>
                    </div>
                `;
            }
        });
    }

    // Добавляем стили для уведомлений
    const style = document.createElement('style');
    style.textContent = `
        .friends-notification {
            animation: slideIn 0.3s ease-out;
        }
        
        @keyframes slideIn {
            from {
                transform: translateX(100%);
                opacity: 0;
            }
            to {
                transform: translateX(0);
                opacity: 1;
            }
        }
        
        .friend-status-accepted {
            border-left: 3px solid #28a745;
        }
        
        .friend-status-pending-sent {
            border-left: 3px solid #ffc107;
        }
        
        .friend-status-pending-received {
            border-left: 3px solid #17a2b8;
        }
        
        .friend-item, .request-item, .search-result-item {
            transition: all 0.2s ease;
        }
        
        .friend-item:hover, .request-item:hover, .search-result-item:hover {
            background-color: #f8f9fa;
        }
        
        .avatar-placeholder {
            font-weight: 600;
            text-transform: uppercase;
        }
        
        .friend-action-buttons {
            display: flex;
            gap: 5px;
        }
    `;
    document.head.appendChild(style);
});

// Экспортируем функции для глобального доступа
window.loadFriends = loadFriends;
window.loadFriendRequests = loadFriendRequests;
window.searchUsers = searchUsers;
window.sendFriendRequest = sendFriendRequest;
window.acceptFriendRequest = acceptFriendRequest;
window.rejectFriendRequest = rejectFriendRequest;
window.acceptFriendRequestFromSearch = acceptFriendRequestFromSearch;
window.rejectFriendRequestFromSearch = rejectFriendRequestFromSearch;
window.removeFriend = removeFriend;
window.startPrivateChat = startPrivateChat;
window.showNotification = showNotification;