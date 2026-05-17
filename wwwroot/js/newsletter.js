// newsletter.js - упрощенная версия
$(document).ready(function () {
    console.log('Newsletter script loaded');

    // Обработка подписки на рассылку
    $('#newsletterForm').on('submit', function (e) {
        e.preventDefault();
        console.log('Form submitted');

        var email = $('#newsletterEmail').val().trim();
        console.log('Email entered:', email);

        // Базовая валидация
        if (!email) {
            showMessage('Введите email адрес', 'danger');
            return;
        }

        // Простая валидация email
        if (!email.includes('@') || !email.includes('.')) {
            showMessage('Введите корректный email адрес', 'danger');
            return;
        }

        // Получаем CSRF токен
        var token = $('input[name="__RequestVerificationToken"]').val();

        // Показываем загрузку
        var btn = $('#subscribeBtn');
        var originalText = btn.html();
        btn.html('<span class="spinner-border spinner-border-sm"></span> Отправка...');
        btn.prop('disabled', true);

        // Отправляем запрос
        $.ajax({
            url: '/Newsletter/Subscribe',
            type: 'POST',
            data: {
                __RequestVerificationToken: token,
                email: email
            },
            success: function (response) {
                console.log('Response:', response);

                if (response.success) {
                    showMessage(response.message, 'success');
                    $('#newsletterEmail').val('');
                } else {
                    // Если уже подписан
                    if (response.alreadySubscribed) {
                        showMessage('Вы уже подписаны на рассылку!', 'info');
                        $('#newsletterEmail').val('');
                    } else {
                        showMessage(response.message || 'Ошибка при подписке', 'danger');
                    }
                }
            },
            error: function (xhr, status, error) {
                console.error('AJAX error:', error);
                showMessage('Ошибка сервера. Попробуйте позже.', 'danger');
            },
            complete: function () {
                btn.html(originalText);
                btn.prop('disabled', false);
            }
        });
    });

    // Функция для показа сообщений
    function showMessage(message, type) {
        var messageDiv = $('#newsletterMessage');

        // Определяем классы и иконки для разных типов сообщений
        var alertClass, icon;
        switch (type) {
            case 'success':
                alertClass = 'alert-success';
                icon = '<i class="fas fa-check-circle me-2"></i>';
                break;
            case 'danger':
                alertClass = 'alert-danger';
                icon = '<i class="fas fa-exclamation-circle me-2"></i>';
                break;
            case 'info':
                alertClass = 'alert-info';
                icon = '<i class="fas fa-info-circle me-2"></i>';
                break;
            case 'warning':
                alertClass = 'alert-warning';
                icon = '<i class="fas fa-exclamation-triangle me-2"></i>';
                break;
            default:
                alertClass = 'alert-info';
                icon = '<i class="fas fa-info-circle me-2"></i>';
        }

        // Удаляем предыдущие сообщения
        messageDiv.find('.alert').alert('close');

        // Создаем сообщение
        var alertHtml = `
            <div class="alert ${alertClass} alert-dismissible fade show" role="alert">
                ${icon} ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
            </div>
        `;

        messageDiv.html(alertHtml);

        // Автоматически скрываем через 5 секунд
        setTimeout(function () {
            messageDiv.find('.alert').alert('close');
        }, 5000);
    }
});