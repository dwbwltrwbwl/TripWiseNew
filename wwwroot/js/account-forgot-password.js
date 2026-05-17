document.addEventListener('DOMContentLoaded', function () {
    // Элементы шагов
    const step1 = document.getElementById('step1');
    const step2 = document.getElementById('step2');
    const step3 = document.getElementById('step3');
    const step4 = document.getElementById('step4');

    // Элементы формы
    const emailInput = document.getElementById('email');
    const hiddenEmail = document.getElementById('hiddenEmail');
    const resetEmail = document.getElementById('resetEmail');
    const emailDisplay = document.getElementById('emailDisplay');
    const verificationCode = document.getElementById('verificationCode');
    const newPassword = document.getElementById('newPassword');
    const confirmPassword = document.getElementById('confirmPassword');

    // Кнопки
    const sendCodeBtn = document.getElementById('sendCodeBtn');
    const verifyCodeBtn = document.getElementById('verifyCodeBtn');
    const resetPasswordBtn = document.getElementById('resetPasswordBtn');
    const resendCodeBtn = document.getElementById('resendCodeBtn');
    const backToStep1Btn = document.getElementById('backToStep1Btn');
    const backToStep2Btn = document.getElementById('backToStep2Btn');

    // Элементы для силы пароля
    const passwordStrengthBar = document.getElementById('passwordStrengthBar');
    const passwordStrengthText = document.getElementById('passwordStrengthText');
    const charCounter = document.getElementById('charCounter');

    // Таймеры
    const timerElement = document.getElementById('timer');
    let codeExpiryTimer = null;
    let resendInterval = null;
    let codeExpirySeconds = 900; // 15 минут
    let resendSeconds = 60;

    // Данные
    let currentEmail = '';

    // Функции переключения видимости пароля
    function togglePasswordVisibility(input, button) {
        const type = input.getAttribute('type') === 'password' ? 'text' : 'password';
        input.setAttribute('type', type);
        button.innerHTML = type === 'password' ? '<i class="fas fa-eye"></i>' : '<i class="fas fa-eye-slash"></i>';
    }

    const toggleNewPassword = document.getElementById('toggleNewPassword');
    const toggleConfirmPassword = document.getElementById('toggleConfirmPassword');

    if (toggleNewPassword) {
        toggleNewPassword.addEventListener('click', function () {
            togglePasswordVisibility(newPassword, this);
        });
    }

    if (toggleConfirmPassword) {
        toggleConfirmPassword.addEventListener('click', function () {
            togglePasswordVisibility(confirmPassword, this);
        });
    }

    // Счетчик символов
    function updateCharCounter() {
        if (charCounter && newPassword) {
            const length = newPassword.value.length;
            charCounter.textContent = length + '/50';

            if (length > 45) {
                charCounter.style.color = '#ffc107';
            } else if (length === 50) {
                charCounter.style.color = '#dc3545';
            } else {
                charCounter.style.color = '#6c757d';
            }
        }
    }

    // Проверка требований к паролю (6 требований: длина, заглавная, строчная, цифра, спецсимвол, макс длина)
    function checkPasswordStrength(password) {
        let strength = 0;
        if (password.length >= 6) strength++;
        if (password.length <= 50) strength++;
        if (/[A-Z]/.test(password)) strength++;
        if (/[a-z]/.test(password)) strength++;
        if (/[0-9]/.test(password)) strength++;
        if (/[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password)) strength++;
        return strength;
    }

    function updatePasswordStrength() {
        const password = newPassword.value;
        const strength = checkPasswordStrength(password);
        const percentage = Math.min((strength / 6) * 100, 100);

        if (passwordStrengthBar) {
            passwordStrengthBar.style.width = percentage + '%';
        }

        let color = '#dc3545';
        let text = 'Очень слабый';

        if (strength >= 2) {
            color = '#ffc107';
            text = 'Слабый';
        }
        if (strength >= 3) {
            color = '#fd7e14';
            text = 'Средний';
        }
        if (strength >= 4) {
            color = '#20c997';
            text = 'Хороший';
        }
        if (strength >= 5) {
            color = '#198754';
            text = 'Отличный';
        }
        if (strength >= 6) {
            color = '#0d6efd';
            text = 'Идеальный';
        }

        if (passwordStrengthBar) {
            passwordStrengthBar.style.backgroundColor = color;
        }
        if (passwordStrengthText) {
            passwordStrengthText.textContent = 'Сила пароля: ' + text;
        }

        // Обновляем требования
        const reqLength = document.getElementById('reqLength');
        const reqMaxLength = document.getElementById('reqMaxLength');
        const reqUppercase = document.getElementById('reqUppercase');
        const reqLowercase = document.getElementById('reqLowercase');
        const reqNumber = document.getElementById('reqNumber');
        const reqSpecial = document.getElementById('reqSpecial');

        if (reqLength) updateRequirement(reqLength, password.length >= 6);
        if (reqMaxLength) updateRequirement(reqMaxLength, password.length <= 50);
        if (reqUppercase) updateRequirement(reqUppercase, /[A-Z]/.test(password));
        if (reqLowercase) updateRequirement(reqLowercase, /[a-z]/.test(password));
        if (reqNumber) updateRequirement(reqNumber, /[0-9]/.test(password));
        if (reqSpecial) updateRequirement(reqSpecial, /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password));
    }

    function updateRequirement(element, isValid) {
        if (isValid) {
            element.classList.add('valid');
            element.classList.remove('text-danger');
            const icon = element.querySelector('i');
            if (icon) icon.className = 'fas fa-check me-1';
        } else {
            element.classList.remove('valid');
            element.classList.add('text-danger');
            const icon = element.querySelector('i');
            if (icon) icon.className = 'fas fa-times me-1';
        }
    }

    function checkPasswordMatch() {
        const password = newPassword ? newPassword.value : '';
        const confirm = confirmPassword ? confirmPassword.value : '';
        const matchElement = document.getElementById('passwordMatch');
        const successElement = document.getElementById('passwordSuccess');

        if (!matchElement || !successElement) return false;

        if (confirm === '') {
            matchElement.classList.add('d-none');
            successElement.classList.add('d-none');
            return false;
        }

        if (password === confirm) {
            matchElement.classList.add('d-none');
            successElement.classList.remove('d-none');
            return true;
        } else {
            matchElement.classList.remove('d-none');
            successElement.classList.add('d-none');
            return false;
        }
    }

    function validateEmail(email) {
        const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return re.test(email);
    }

    function validateStep3() {
        if (!newPassword) return false;
        const password = newPassword.value;
        const passwordValid = password.trim() !== '' &&
            password.length >= 6 &&
            password.length <= 50 &&
            /[A-Z]/.test(password) &&
            /[a-z]/.test(password) &&
            /[0-9]/.test(password) &&
            /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password);
        const confirmValid = checkPasswordMatch();
        return passwordValid && confirmValid;
    }

    function goToStep(stepNumber) {
        if (step1) step1.classList.add('d-none');
        if (step2) step2.classList.add('d-none');
        if (step3) step3.classList.add('d-none');
        if (step4) step4.classList.add('d-none');

        if (stepNumber === 1) {
            if (step1) step1.classList.remove('d-none');
            stopTimers();
        } else if (stepNumber === 2) {
            if (step2) step2.classList.remove('d-none');
            startTimers();
        } else if (stepNumber === 3) {
            if (step3) step3.classList.remove('d-none');
            updatePasswordStrength();
        } else if (stepNumber === 4) {
            if (step4) step4.classList.remove('d-none');
            stopTimers();
        }
    }

    function stopTimers() {
        if (codeExpiryTimer) {
            clearInterval(codeExpiryTimer);
            codeExpiryTimer = null;
        }
        if (resendInterval) {
            clearInterval(resendInterval);
            resendInterval = null;
        }
    }

    function startTimers() {
        stopTimers();

        codeExpirySeconds = 900;
        updateTimerDisplay();

        codeExpiryTimer = setInterval(function () {
            if (codeExpirySeconds <= 0) {
                clearInterval(codeExpiryTimer);
                if (timerElement) timerElement.textContent = '00:00';
                alert('Время действия кода истекло. Пожалуйста, запросите новый код.');
                goToStep(1);
                return;
            }
            codeExpirySeconds--;
            updateTimerDisplay();
        }, 1000);

        resendSeconds = 60;
        if (resendCodeBtn) {
            resendCodeBtn.disabled = true;
        }

        resendInterval = setInterval(function () {
            resendSeconds--;
            if (resendTimerElement) resendTimerElement.textContent = resendSeconds;

            if (resendSeconds <= 0) {
                clearInterval(resendInterval);
                if (resendCodeBtn) {
                    resendCodeBtn.disabled = false;
                    resendCodeBtn.innerHTML = 'Отправить код повторно';
                }
            } else if (resendCodeBtn) {
                resendCodeBtn.innerHTML = 'Отправить код повторно (<span id="resendTimer">' + resendSeconds + '</span>)';
            }
        }, 1000);
    }

    function updateTimerDisplay() {
        if (timerElement) {
            const minutes = Math.floor(codeExpirySeconds / 60);
            const seconds = codeExpirySeconds % 60;
            timerElement.textContent = minutes.toString().padStart(2, '0') + ':' + seconds.toString().padStart(2, '0');
        }
    }

    // Отправка кода на email
    const emailForm = document.getElementById('emailForm');
    if (emailForm) {
        emailForm.addEventListener('submit', async function (e) {
            e.preventDefault();

            const email = emailInput ? emailInput.value.trim() : '';

            if (!email) {
                showError('emailError', 'Введите email адрес');
                return;
            }

            if (!validateEmail(email)) {
                showError('emailError', 'Введите корректный email адрес');
                return;
            }

            if (sendCodeBtn) {
                sendCodeBtn.disabled = true;
                sendCodeBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Отправка...';
            }

            try {
                const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
                const token = tokenInput ? tokenInput.value : '';

                const response = await fetch('/Account/SendResetCode', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify({ email: email })
                });

                const result = await response.json();

                if (result.success) {
                    currentEmail = email;
                    if (hiddenEmail) hiddenEmail.value = email;
                    if (resetEmail) resetEmail.value = email;
                    if (emailDisplay) emailDisplay.textContent = email;
                    hideError('emailError');
                    if (verificationCode) verificationCode.value = '';
                    goToStep(2);
                } else {
                    showError('emailError', result.message || 'Ошибка при отправке кода');
                }
            } catch (error) {
                console.error('Ошибка:', error);
                showError('emailError', 'Произошла ошибка при отправке кода');
            } finally {
                if (sendCodeBtn) {
                    sendCodeBtn.disabled = false;
                    sendCodeBtn.innerHTML = 'Отправить код';
                }
            }
        });
    }

    // Проверка кода
    const codeForm = document.getElementById('codeForm');
    if (codeForm) {
        codeForm.addEventListener('submit', async function (e) {
            e.preventDefault();

            const code = verificationCode ? verificationCode.value.trim() : '';

            if (!code || code.length !== 6) {
                showError('codeError', 'Введите 6-значный код');
                return;
            }

            if (verifyCodeBtn) {
                verifyCodeBtn.disabled = true;
                verifyCodeBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Проверка...';
            }

            try {
                const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
                const token = tokenInput ? tokenInput.value : '';

                const response = await fetch('/Account/VerifyResetCode', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify({
                        email: currentEmail,
                        code: code
                    })
                });

                const result = await response.json();

                if (result.success) {
                    hideError('codeError');
                    goToStep(3);
                } else {
                    showError('codeError', result.message || 'Неверный код');
                }
            } catch (error) {
                console.error('Ошибка:', error);
                showError('codeError', 'Произошла ошибка при проверке кода');
            } finally {
                if (verifyCodeBtn) {
                    verifyCodeBtn.disabled = false;
                    verifyCodeBtn.innerHTML = '<i class="fas fa-check me-2"></i>Подтвердить код';
                }
            }
        });
    }

    // Сброс пароля
    const newPasswordForm = document.getElementById('newPasswordForm');
    if (newPasswordForm) {
        newPasswordForm.addEventListener('submit', async function (e) {
            e.preventDefault();

            if (!validateStep3()) {
                showError('passwordFormError', 'Пожалуйста, проверьте правильность ввода нового пароля');
                return;
            }

            if (resetPasswordBtn) {
                resetPasswordBtn.disabled = true;
                resetPasswordBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Сохранение...';
            }

            try {
                const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
                const token = tokenInput ? tokenInput.value : '';

                const response = await fetch('/Account/ResetPassword', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify({
                        email: currentEmail,
                        newPassword: newPassword ? newPassword.value : ''
                    })
                });

                const result = await response.json();

                if (result.success) {
                    goToStep(4);
                } else {
                    showError('passwordFormError', result.message || 'Ошибка при смене пароля');
                }
            } catch (error) {
                console.error('Ошибка:', error);
                showError('passwordFormError', 'Произошла ошибка при смене пароля');
            } finally {
                if (resetPasswordBtn) {
                    resetPasswordBtn.disabled = false;
                    resetPasswordBtn.innerHTML = '<i class="fas fa-save me-2"></i>Сохранить новый пароль';
                }
            }
        });
    }

    // Повторная отправка кода
    if (resendCodeBtn) {
        resendCodeBtn.addEventListener('click', async function () {
            if (this.disabled) return;

            this.disabled = true;
            this.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Отправка...';

            try {
                const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
                const token = tokenInput ? tokenInput.value : '';

                const response = await fetch('/Account/SendResetCode', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify({ email: currentEmail })
                });

                const result = await response.json();

                if (result.success) {
                    startTimers();
                    hideError('codeError');
                } else {
                    alert(result.message || 'Ошибка при отправке кода');
                    this.disabled = false;
                    this.innerHTML = 'Отправить код повторно';
                }
            } catch (error) {
                console.error('Ошибка:', error);
                alert('Произошла ошибка при отправке кода');
                this.disabled = false;
                this.innerHTML = 'Отправить код повторно';
            }
        });
    }

    // Обработчики для кнопок "Назад"
    if (backToStep1Btn) {
        backToStep1Btn.addEventListener('click', function () {
            goToStep(1);
        });
    }

    if (backToStep2Btn) {
        backToStep2Btn.addEventListener('click', function () {
            goToStep(2);
        });
    }

    // Ввод кода (только цифры)
    if (verificationCode) {
        verificationCode.addEventListener('input', function () {
            let code = this.value.replace(/\D/g, '');
            if (code.length > 6) code = code.slice(0, 6);
            this.value = code;

            if (code.length === 6) {
                hideError('codeError');
                if (verifyCodeBtn) verifyCodeBtn.disabled = false;
            } else {
                if (verifyCodeBtn) verifyCodeBtn.disabled = true;
            }
        });
    }

    // Проверка пароля в реальном времени
    if (newPassword) {
        newPassword.addEventListener('input', function () {
            if (this.value.length > 50) {
                this.value = this.value.slice(0, 50);
            }
            updateCharCounter();
            updatePasswordStrength();
            if (resetPasswordBtn) resetPasswordBtn.disabled = !validateStep3();
        });
    }

    if (confirmPassword) {
        confirmPassword.addEventListener('input', function () {
            if (this.value.length > 50) {
                this.value = this.value.slice(0, 50);
            }
            checkPasswordMatch();
            if (resetPasswordBtn) resetPasswordBtn.disabled = !validateStep3();
        });
    }

    // Вспомогательные функции для ошибок
    function showError(elementId, message) {
        const errorElement = document.getElementById(elementId);
        if (errorElement) {
            const messageSpan = document.getElementById(elementId + 'Message');
            if (messageSpan) messageSpan.textContent = message;
            errorElement.classList.remove('d-none');
        }
    }

    function hideError(elementId) {
        const errorElement = document.getElementById(elementId);
        if (errorElement) {
            errorElement.classList.add('d-none');
        }
    }

    // Инициализация
    updateCharCounter();
});