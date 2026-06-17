// ════════════════════════════════════════════════════════
//  catalogo.js — Catálogo conectado a la base de datos
// ════════════════════════════════════════════════════════

let productos = [];   // se llena desde la BD
let carrito = [];
let catActual = 'todos';
let busqueda = '';
let ordenActual = 'nombre';

// ── Cargar productos desde la BD ─────────────────────────
async function cargarProductos() {
    try {
        const res = await fetch('/Inventario/GetProductosJson');
        productos = await res.json();
        renderGrid();
        renderSidebarCats();
    } catch (e) {
        console.error('Error cargando productos:', e);
        document.getElementById('productosGrid').innerHTML =
            '<p style="color:#dc2626;padding:20px;">Error cargando productos.</p>';
    }
}

// ── Categorías dinámicas en sidebar ──────────────────────
function renderSidebarCats() {
    const cats = ['todos', ...new Set(productos.map(p => p.categoria))];
    const cont = document.getElementById('listaCategorias');
    if (!cont) return;

    cont.innerHTML = cats.map(c => `
        <div class="cat-sb-cat ${c === catActual ? 'on' : ''}"
             onclick="filtrar('${c}', this)">
            ${c === 'todos' ? 'Todos los productos' : c}
            <span class="arr">→</span>
        </div>
    `).join('');
}

// ── Utilidades ───────────────────────────────────────────
function fmtPrecio(p) {
    return '$' + Number(p).toLocaleString('es-CO');
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
    const grid = document.getElementById('productosGrid');
    if (!grid) return;

    let lista = [...productos];

    if (catActual !== 'todos')
        lista = lista.filter(p => p.categoria === catActual);

    if (busqueda)
        lista = lista.filter(p =>
            p.nombre.toLowerCase().includes(busqueda) ||
            p.marca.toLowerCase().includes(busqueda)
        );

    lista.sort((a, b) => {
        if (ordenActual === 'precio-asc') return a.precio - b.precio;
        if (ordenActual === 'precio-desc') return b.precio - a.precio;
        return a.nombre.localeCompare(b.nombre);
    });

    if (lista.length === 0) {
        grid.innerHTML = `
            <div style="grid-column:1/-1;text-align:center;padding:60px 20px;color:#9ca3af;">
                <div style="font-size:2.5rem;margin-bottom:12px;">🔍</div>
                <p>No se encontraron repuestos</p>
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
                    <button class="cat-btn-detalles"
                            onclick="verDetalle(${p.id})">
                        Ver detalles
                    </button>
                    <button class="cat-btn-agregar"
                            id="btn-${p.id}"
                            ${agotado ? 'disabled' : ''}
                            onclick="toggleItem(${p.id}, '${p.nombre.replace(/'/g, "\\'")}', ${p.precio})">
                        ${agotado ? 'Agotado' : inc ? '✓ Agregado' : '+ Cotizar'}
                    </button>
                </div>
            </div>
        </div>`;
    }).join('');
}

// ── Filtrar por categoría ────────────────────────────────
function filtrar(cat, el) {
    catActual = cat;
    document.querySelectorAll('.cat-sb-cat')
        .forEach(e => e.classList.remove('on'));
    if (el) el.classList.add('on');
    renderGrid();
}

// ── Buscar ───────────────────────────────────────────────
function buscar() {
    busqueda = document.getElementById('searchInp').value.toLowerCase();
    renderGrid();
}

// ── Ordenar ──────────────────────────────────────────────
function ordenar(val) {
    ordenActual = val;
    renderGrid();
}

// ── Ver detalle ──────────────────────────────────────────
function verDetalle(id) {
    window.location.href = '/Inventario/Detalles/' + id;
}

// ════════════════════════════════════════
// CARRITO / COTIZACIÓN
// ════════════════════════════════════════
function toggleItem(id, nombre, precio) {
    const idx = carrito.findIndex(c => c.id === id);
    if (idx >= 0) {
        carrito.splice(idx, 1);
    } else {
        carrito.push({ id, nombre, precio });
    }
    actualizarCarrito();
    renderGrid();
}

function toggleCarrito() {
    const panel = document.getElementById('carritoPanel');
    if (panel)
        panel.style.display = panel.style.display === 'none' ? 'block' : 'none';
}

function actualizarCarrito() {
    const badge = document.getElementById('badgeCount');
    const sbCount = document.getElementById('sbCount');
    const items = document.getElementById('carritoItems');
    const totalVal = document.getElementById('totalVal');

    if (badge) badge.textContent = carrito.length;
    if (sbCount) sbCount.textContent = carrito.length;

    if (items) {
        if (carrito.length === 0) {
            items.innerHTML = `
                <p style="padding:16px;text-align:center;
                           color:#9ca3af;font-size:.82rem;">
                    Sin productos agregados
                </p>`;
        } else {
            items.innerHTML = carrito.map(c => `
                <div class="cat-carrito-item">
                    <span class="cat-carrito-item-name">${c.nombre}</span>
                    <span class="cat-carrito-item-precio">
                        ${fmtPrecio(c.precio)}
                    </span>
                    <button class="cat-carrito-item-x"
                            onclick="toggleItem(${c.id},'','')">✕</button>
                </div>`).join('');
        }
    }

    const total = carrito.reduce((s, c) => s + Number(c.precio), 0);
    if (totalVal) totalVal.textContent = fmtPrecio(total);
}

// ── Iniciar al cargar la página ──────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    cargarProductos();
    actualizarCarrito();
});