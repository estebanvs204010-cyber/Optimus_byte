// ════════════════════════════════════════════════════════
//  catalogo.js  —  Lógica del catálogo de repuestos
//  Ubicación: VistaPrincipal/wwwroot/js/catalogo.js
// ════════════════════════════════════════════════════════

// ── Datos de productos ───────────────────────────────────
// TODO: cuando tengas BD, reemplaza este array por una
//       llamada fetch('/Inventario/GetProductos') y usa
//       el JSON que devuelva tu controlador.
const productos = [
    { id: 1, nombre: 'Aceite de motor 5W-30', marca: 'Mobil', categoria: 'Lubricantes', desc: 'Lubricante sintético para motores modernos.', precio: 85000, stock: 10, bg: 'bg-motor', icon: '🛢️' },
    { id: 2, nombre: 'Filtro de aire', marca: 'Bosch', categoria: 'Filtros', desc: 'Mantiene limpio el ingreso de aire al motor.', precio: 35000, stock: 15, bg: 'bg-filtros', icon: '🌬️' },
    { id: 3, nombre: 'Pastillas de freno', marca: 'Brembo', categoria: 'Frenos', desc: 'Pastillas para sistema de frenos delanteros.', precio: 120000, stock: 8, bg: 'bg-frenos', icon: '🔴' },
    { id: 4, nombre: 'Batería 12V', marca: 'Willard', categoria: 'Eléctrico', desc: 'Batería automotriz para autos particulares.', precio: 280000, stock: 3, bg: 'bg-electrico', icon: '🔋' },
    { id: 5, nombre: 'Bujías de encendido', marca: 'NGK', categoria: 'Motor', desc: 'Bujías de iridio de alto rendimiento.', precio: 45000, stock: 12, bg: 'bg-motor', icon: '⚡' },
    { id: 6, nombre: 'Amortiguador delantero', marca: 'Monroe', categoria: 'Suspensión', desc: 'Amortiguador hidráulico eje delantero.', precio: 210000, stock: 2, bg: 'bg-suspension', icon: '🔧' },
    { id: 7, nombre: 'Correa de distribución', marca: 'Gates', categoria: 'Motor', desc: 'Correa dentada para sincronización del motor.', precio: 95000, stock: 6, bg: 'bg-motor', icon: '⚙️' },
    { id: 8, nombre: 'Filtro de aceite', marca: 'Mann', categoria: 'Filtros', desc: 'Filtra impurezas del aceite del motor.', precio: 28000, stock: 20, bg: 'bg-filtros', icon: '🔩' },
    { id: 9, nombre: 'Líquido de frenos DOT 4', marca: 'Castrol', categoria: 'Frenos', desc: 'Fluido hidráulico para sistema de frenos.', precio: 22000, stock: 0, bg: 'bg-frenos', icon: '💧' },
];

// ── Estado de la app ─────────────────────────────────────
let carrito = [];
let catActual = 'todos';
let busqueda = '';
let ordenActual = 'nombre';

// ── Utilidades ───────────────────────────────────────────
function fmtPrecio(p) {
    return '$' + p.toLocaleString('es-CO');
}

function getBadge(p) {
    if (p.stock === 0) return '<span class="cat-badge agotado">Agotado</span>';
    if (p.stock <= 3) return '<span class="cat-badge bajo">Stock bajo</span>';
    return '<span class="cat-badge">Disponible</span>';
}

function enCarrito(id) {
    return carrito.some(c => c.id === id);
}

// ── Renderizado del grid ─────────────────────────────────
function renderGrid() {
    let lista = [...productos];

    // Filtrar categoría
    if (catActual !== 'todos') {
        lista = lista.filter(p => p.categoria === catActual);
    }

    // Filtrar búsqueda
    if (busqueda) {
        lista = lista.filter(p =>
            p.nombre.toLowerCase().includes(busqueda) ||
            p.marca.toLowerCase().includes(busqueda)
        );
    }

    // Ordenar
    if (ordenActual === 'precio-asc') lista.sort((a, b) => a.precio - b.precio);
    else if (ordenActual === 'precio-desc') lista.sort((a, b) => b.precio - a.precio);
    else lista.sort((a, b) => a.nombre.localeCompare(b.nombre));

    const grid = document.getElementById('productosGrid');
    if (!grid) return;

    if (lista.length === 0) {
        grid.innerHTML = `<div style="grid-column:1/-1;padding:40px;text-align:center;color:var(--texto-suave);font-size:.9rem;">
            No se encontraron productos para esta búsqueda.
        </div>`;
        return;
    }

    grid.innerHTML = lista.map(p => {
        const inc = enCarrito(p.id);
        const agotado = p.stock === 0;
        return `
        <div class="cat-card">
            <div class="cat-card-img ${p.bg}">
                <span style="font-size:2.8rem;">${p.icon}</span>
                ${getBadge(p)}
            </div>
            <div class="cat-card-body">
                <div class="cat-card-nombre">${p.nombre}</div>
                <div class="cat-card-marca">${p.marca} · ${p.categoria}</div>
                <div class="cat-card-desc">${p.desc}</div>
                <div class="cat-card-precio">${fmtPrecio(p.precio)} <span>COP</span></div>
                <div class="cat-card-btns">
                    <button class="cat-btn-detalles" onclick="verDetalle(${p.id})">Ver detalles</button>
                    <button class="cat-btn-agregar" id="btn-${p.id}"
                            ${agotado ? 'disabled' : ''}
                            onclick="toggleItem(${p.id}, '${p.nombre.replace(/'/g, "\\'")}', ${p.precio})">
                        ${agotado ? 'Agotado' : inc ? '✓ Agregado' : '+ Cotizar'}
                    </button>
                </div>
            </div>
        </div>`;
    }).join('');
}

// ── Carrito / Cotización ─────────────────────────────────
function toggleItem(id, nombre, precio) {
    if (enCarrito(id)) {
        carrito = carrito.filter(c => c.id !== id);
    } else {
        carrito.push({ id, nombre, precio });
    }
    renderCarrito();
    renderGrid();
}

function renderCarrito() {
    const n = carrito.length;

    const badge = document.getElementById('badgeCount');
    const sbCount = document.getElementById('sbCount');
    if (badge) badge.textContent = n;
    if (sbCount) sbCount.textContent = n;

    const items = document.getElementById('carritoItems');
    if (!items) return;

    items.innerHTML = carrito.length === 0
        ? '<div style="padding:16px;font-size:.78rem;color:var(--texto-suave);text-align:center;">Sin productos agregados</div>'
        : carrito.map(c => `
            <div class="cat-carrito-item">
                <div>
                    <div class="cat-carrito-item-name">${c.nombre}</div>
                    <div class="cat-carrito-item-precio">${fmtPrecio(c.precio)}</div>
                </div>
                <button class="cat-carrito-item-x"
                        onclick="toggleItem(${c.id}, '', 0)">✕</button>
            </div>`).join('');

    const total = carrito.reduce((s, c) => s + c.precio, 0);
    const totalEl = document.getElementById('totalVal');
    if (totalEl) totalEl.textContent = fmtPrecio(total);
}

function toggleCarrito() {
    const panel = document.getElementById('carritoPanel');
    if (!panel) return;
    const abierto = panel.style.display !== 'none';
    panel.style.display = abierto ? 'none' : 'block';
    if (!abierto) renderCarrito();
}

// ── Filtros ──────────────────────────────────────────────
function filtrar(cat, el) {
    catActual = cat;
    document.querySelectorAll('.cat-sb-cat').forEach(e => e.classList.remove('on'));
    el.classList.add('on');
    renderGrid();
}

function buscar() {
    const inp = document.getElementById('searchInp');
    busqueda = inp ? inp.value.toLowerCase().trim() : '';
    renderGrid();
}

function ordenar(v) {
    ordenActual = v;
    renderGrid();
}

// ── Detalle de producto ──────────────────────────────────
// Por ahora muestra un alert. Cuando quieras una vista
// real, cambia esto por: window.location.href = '/Inventario/Detalles/' + id;
function verDetalle(id) {
    const p = productos.find(x => x.id === id);
    if (!p) return;
    alert(
        `📦 ${p.nombre}\n\n` +
        `🏷️  Marca: ${p.marca}\n` +
        `📂  Categoría: ${p.categoria}\n` +
        `📝  ${p.desc}\n` +
        `💰  Precio: ${fmtPrecio(p.precio)} COP\n` +
        `📦  Stock: ${p.stock} unidades`
    );
}

// ── Inicialización ───────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    renderGrid();
    renderCarrito();

    const btn = document.getElementById('toggleCarrito');
    if (btn) btn.addEventListener('click', toggleCarrito);
});