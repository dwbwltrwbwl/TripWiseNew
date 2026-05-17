$(document).ready(function () {
    // Элементы формы
    const emailForm = $('#emailForm');
    const subscriptionResult = $('#subscriptionResult');
    const successMessage = $('#successMessage');
    const loadingSpinner = $('#loadingSpinner');
    const emailInput = $('#emailInput');
    const displayEmail = $('#displayEmail');
    const subscribedDate = $('#subscribedDate');
    const statusAlert = $('#statusAlert');
    const statusMessage = $('#statusMessage');

    // Проверка подписки
    $('#checkSubscriptionBtn').click(function () {
        const email = emailInput.val().trim();

        if (!email) {
            showError('Введите email адрес');
            return;
        }

        if (!isValidEmail(email)) {
            showError('Введите корректный email адрес');
            return;
        }

        checkSubscription(email);
    });

    // Нажатие Enter в поле email
    emailInput.keypress(function (e) {
        if (e.which === 13) {
            $('#checkSubscriptionBtn').click();
        }
    });

    // Кнопка "Назад"
    $('#backBtn').click(function () {
        subscriptionResult.hide();
        emailForm.show();
        emailInput.val('').focus();
    });

    // Кнопка отписки
    $('#unsubscribeBtn').click(function () {
        const email = emailInput.val().trim();
        unsubscribe(email);
    });

    // Функция проверки подписки
    function checkSubscription(email) {
        emailForm.hide();
        loadingSpinner.show();

        $.ajax({
            url: '/Newsletter/CheckSubscription',
            type: 'GET',
            data: { email: email },
            success: function (response) {
                loadingSpinner.hide();

                if (response.error) {
                    showError(response.error);
                    emailForm.show();
                    return;
                }

                displayEmail.text(response.email);

                if (response.isSubscribed) {
                    statusAlert.removeClass('alert-warning alert-danger')
                        .addClass('alert-success');
                    statusMessage.html('<i class="fas fa-check-circle me-2"></i>Вы подписаны на рассылку');
                    subscribedDate.text(`Подписан: ${response.subscribedAt || 'дата неизвестна'}`);
                } else {
                    statusAlert.removeClass('alert-success alert-danger')
                        .addClass('alert-warning');
                    statusMessage.html('<i class="fas fa-exclamation-triangle me-2"></i>Вы не подписаны на рассылку');
                    subscribedDate.text('Активная подписка отсутствует');
                }

                subscriptionResult.show();
            },
            error: function () {
                loadingSpinner.hide();
                showError('Ошибка при проверке подписки');
                emailForm.show();
            }
        });
    }

    // Функция отписки
    function unsubscribe(email) {
        if (!confirm('Вы уверены, что хотите отписаться от рассылки? Вы больше не будете получать уведомления о скидках и специальных предложениях.')) {
            return;
        }

        subscriptionResult.hide();
        loadingSpinner.show();

        $.ajax({
            url: '/Newsletter/Unsubscribe',
            type: 'POST',
            data: {
                __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val(),
                email: email
            },
            success: function (response) {
                loadingSpinner.hide();

                if (response.success) {
                    showSuccess('Вы успешно отписались от рассылки', response.message);
                } else {
                    subscriptionResult.show();
                    alert(response.message || 'Ошибка при отписке');
                }
            },
            error: function () {
                loadingSpinner.hide();
                subscriptionResult.show();
                alert('Ошибка сервера. Попробуйте позже.');
            }
        });
    }

    // Вспомогательные функции
    function isValidEmail(email) {
        const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return re.test(email);
    }

    function showError(message) {
        alert(message);
    }

    function showSuccess(title, details) {
        emailForm.hide();
        subscriptionResult.hide();
        $('#successText').text(title);
        $('#successDetails').text(details || 'Спасибо, что были с нами!');
        successMessage.show();
    }
});