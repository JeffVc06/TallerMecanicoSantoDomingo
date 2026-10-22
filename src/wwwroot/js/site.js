/* Funcionalidad general del sistema (sin datos en memoria: todo se consulta al servidor). */

// ===== Aparición progresiva de secciones =====
(function () {
    const elementos = document.querySelectorAll(
        '.encabezado-pagina, .pagina > .tarjeta, .pagina > .row, .pagina > .alerta-info, .pagina > .alerta-error, .pagina > .alerta-exito'
    );
    const movimientoReducido = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (movimientoReducido || elementos.length === 0) return;

    elementos.forEach(function (elemento, indice) {
        elemento.classList.add('revelar');
        elemento.style.setProperty('--retraso', Math.min((indice % 4) * 55, 165) + 'ms');
    });

    if (!('IntersectionObserver' in window)) {
        elementos.forEach(function (elemento) { elemento.classList.add('visible'); });
        return;
    }

    const observador = new IntersectionObserver(function (entradas, instancia) {
        entradas.forEach(function (entrada) {
            if (entrada.isIntersecting) {
                entrada.target.classList.add('visible');
                instancia.unobserve(entrada.target);
            }
        });
    }, { threshold: .08, rootMargin: '0px 0px -24px 0px' });

    elementos.forEach(function (elemento) { observador.observe(elemento); });
})();

// ===== Menú adaptable =====
(function () {
    const boton = document.getElementById('boton-menu');
    const barra = document.getElementById('barra-lateral');
    const fondo = document.getElementById('fondo-menu');

    function cerrarMenu() {
        if (!boton || !barra) return;
        barra.classList.remove('abierta');
        fondo?.classList.remove('visible');
        boton.setAttribute('aria-expanded', 'false');
        boton.setAttribute('aria-label', 'Abrir menú');
        document.body.classList.remove('menu-abierto');
    }

    if (boton && barra) {
        boton.addEventListener('click', function () {
            const abierto = !barra.classList.contains('abierta');
            barra.classList.toggle('abierta', abierto);
            fondo?.classList.toggle('visible', abierto);
            boton.setAttribute('aria-expanded', String(abierto));
            boton.setAttribute('aria-label', abierto ? 'Cerrar menú' : 'Abrir menú');
            document.body.classList.toggle('menu-abierto', abierto);
        });

        fondo?.addEventListener('click', cerrarMenu);
        document.addEventListener('click', function (evento) {
            if (barra.classList.contains('abierta') &&
                !barra.contains(evento.target) &&
                !boton.contains(evento.target)) {
                cerrarMenu();
            }
        });

        document.addEventListener('keydown', function (evento) {
            if (evento.key === 'Escape' && barra.classList.contains('abierta')) {
                cerrarMenu();
                boton.focus();
            }
        });

        barra.querySelectorAll('a').forEach(function (enlace) {
            enlace.addEventListener('click', cerrarMenu);
        });

        window.addEventListener('resize', function () {
            if (window.innerWidth >= 992) cerrarMenu();
        });
    }
})();

// ===== Confirmaciones de acciones sensibles =====
document.addEventListener('click', function (evento) {
    const elemento = evento.target.closest('[data-confirmar]');
    if (elemento) {
        if (!window.confirm(elemento.getAttribute('data-confirmar'))) {
            evento.preventDefault();
            evento.stopPropagation();
        }
    }
});

// ===== Mostrar u ocultar contraseña =====
document.addEventListener('click', function (evento) {
    const boton = evento.target.closest('[data-ver-password]');
    if (boton) {
        const campo = document.getElementById(boton.getAttribute('data-ver-password'));
        if (campo) {
            const visible = campo.type === 'text';
            campo.type = visible ? 'password' : 'text';
            boton.querySelector('i').className = visible ? 'bi bi-eye' : 'bi bi-eye-slash';
        }
    }
});

// ===== Utilidad: token antiforgery para peticiones fetch =====
function obtenerTokenAntiforgery() {
    const meta = document.querySelector('meta[name="csrf-token"]');
    return meta ? meta.content : '';
}

// ===== Formateador de moneda nicaragüense =====
function formatearMoneda(valor) {
    const numero = Number(valor);
    return 'C$ ' + numero.toLocaleString('es-NI', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

// ===== Búsqueda con filtro de formulario =====
document.addEventListener('submit', function (evento) {
    const formulario = evento.target;
    if (formulario.matches('[data-busqueda]')) {
        const campoPagina = formulario.querySelector('input[name="pagina"]');
        if (campoPagina) campoPagina.value = '1';
    }
});
