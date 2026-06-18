// ════════════════════════════════════════════════════════
//  catalogo.js  —  Catálogo Optimus-Byte
//  Carga desde /Inventario/GetProductos (JSON real de BD)
// ════════════════════════════════════════════════════════

// ── Imágenes por defecto según categoría ─────────────────
// Pon estos archivos en wwwroot/img/defaults/
const IMG_DEFAULTS = {
    'Motor'           : '/img/defaults/motor.png',
    'Frenos'          : '/img/defaults/frenos.png',
    'Filtros'         : '/img/defaults/filtros.png',
    'Eléctrico'       : '/img/defaults/electrico.png',
    'Sistema Eléctrico': '/img/defaults/electrico.png',
    'Suspensión'      : '/img/defaults/suspension.png',
    'Lubricantes'     : '/img/defaults/lubricantes.png',
    '__default'       : '/img/defaults/repuesto.png'
};

// Colores de fondo por categoría (para cuando no hay imagen)
const BG_CAT = {
    'Motor'            : 'bg-motor',
    'Frenos'           : 'bg-frenos',
    'Filtros'          : 'bg-filtros',
    'Eléctrico'        : 'bg-electrico',
    'Sistema Eléctrico': 'bg-electrico',
    'Suspensión'       : 'bg-suspension',
    'Lubricantes'      : 'bg-lubricante',
};

// ── Estado de la app ─────────────────────────────────────
let productos   = [];
let carrito     = [];
let catActual   = 'todos';
let marcaActual = 'todas';
let modeloActual= 'todos';
let busqueda    = '';
let ordenActual = 'nombre';

// ── Cargar productos desde BD ────────────────────────────
async function cargarProductos() {
    try {
        const res  = await fetch('/Inventario/GetProductos');
        productos  = await res.json();
        poblarFiltrosMarcaModelo();
        poblarCategoriasSidebar(); 
        renderGrid();
    } catch (e) {
        console.error('Error al cargar productos:', e);
        const grid = document.getElementById('productosGrid');
        if (grid) grid.innerHTML = `
            <div style="grid-column:1/-1;padding:40px;text-align:center;color:#dc2626;">
                No se pudieron cargar los productos. Intenta de nuevo.
            </div>`;
    }
}

// ── Construir filtros dinámicos de Marca y Modelo ────────
function poblarFiltrosMarcaModelo() {
    const marcas  = [...new Set(productos.map(p => p.marca).filter(Boolean))].sort();
    const modelos = [...new Set(productos.map(p => p.modelo).filter(Boolean))].sort();

    const selMarca  = document.getElementById('filtroMarca');
    const selModelo = document.getElementById('filtroModelo');

    if (selMarca) {
        selMarca.innerHTML = '<option value="todas">Todas las marcas</option>';
        marcas.forEach(m => {
            const opt = document.createElement('option');
            opt.value = m; opt.textContent = m;
            selMarca.appendChild(opt);
        });
    }
    if (selModelo) {
        selModelo.innerHTML = '<option value="todos">Todos los modelos</option>';
        modelos.forEach(m => {
            const opt = document.createElement('option');
            opt.value = m; opt.textContent = m;
            selModelo.appendChild(opt);
        });
    }
}

function poblarCategoriasSidebar() {
    const cats = [...new Set(productos.map(p => p.categoria).filter(Boolean))].sort();
    const contenedor = document.getElementById('listaCategorias');
    if (!contenedor) return;

    // Mantener el "Todos" que ya está
    const todosDiv = contenedor.querySelector('.cat-sb-cat');

    contenedor.innerHTML = '';
    contenedor.appendChild(todosDiv);

    cats.forEach(cat => {
        const div = document.createElement('div');
        div.className = 'cat-sb-cat';
        div.innerHTML = `${cat} <span class="arr">→</span>`;
        div.onclick = function () { filtrar(cat, this); };
        contenedor.appendChild(div);
    });
}


// Actualizar modelos disponibles según marca seleccionada
function actualizarModelos() {
    const marcaSel = document.getElementById('filtroMarca')?.value || 'todas';
    const fuente   = marcaSel === 'todas'
        ? productos
        : productos.filter(p => p.marca === marcaSel);
    const modelos  = [...new Set(fuente.map(p => p.modelo).filter(Boolean))].sort();
    const selMod   = document.getElementById('filtroModelo');
    if (!selMod) return;
    selMod.innerHTML = '<option value="todos">Todos los modelos</option>';
    modelos.forEach(m => {
        const opt = document.createElement('option');
        opt.value = m; opt.textContent = m;
        selMod.appendChild(opt);
    });
    modeloActual = 'todos';
}

// ── Utilidades ───────────────────────────────────────────
function fmtPrecio(p) {
    return '$' + Number(p).toLocaleString('es-CO');
}

function getBadge(p) {
    if (p.stock === 0)  return '<span class="cat-badge agotado">Agotado</span>';
    if (p.stock <= 3)   return '<span class="cat-badge bajo">Stock bajo</span>';
    return '<span class="cat-badge">Disponible</span>';
}

function enCarrito(id) {
    return carrito.some(c => c.id === id);
}

// Obtener imagen: usa la propia si existe, sino default por categoría
function getImagen(p) {
    if (p.imagenUrl && p.imagenUrl.trim() !== '') {
        return `<img src="${p.imagenUrl}" alt="${p.nombre}"
                     style="width:100%;height:100%;object-fit:cover;"
                     onerror="this.parentElement.innerHTML=getIconoFallback('${p.categoria}')">`;
    }
    const imgDef = IMG_DEFAULTS[p.categoria] || IMG_DEFAULTS['__default'];
    const bgCls  = BG_CAT[p.categoria] || 'bg-motor';
    // Si el archivo de default existe, úsalo; si no, muestra SVG inline
    return `<img src="${imgDef}" alt="${p.categoria}"
                 style="width:80%;height:80%;object-fit:contain;opacity:.45;"
                 onerror="this.parentElement.innerHTML=getIconoFallback('${p.categoria}')">`;
}

function getIconoFallback(cat) {
    const iconos = {
        'Motor'            : '⚙️',
        'Frenos'           : '🛞',
        'Filtros'          : '🌬️',
        'Eléctrico'        : '🔋',
        'Sistema Eléctrico': '🔋',
        'Suspensión'       : '🔧',
        'Lubricantes'      : '🛢️',
    };
    const icono = iconos[cat] || '🔩';
    return `<span style="font-size:2.8rem;">${icono}</span>`;
}

// ── Renderizado del grid ─────────────────────────────────
function renderGrid() {
    let lista = [...productos];

    // Filtrar categoría
    if (catActual !== 'todos')
        lista = lista.filter(p => p.categoria === catActual);

    // Filtrar marca
    if (marcaActual !== 'todas')
        lista = lista.filter(p => p.marca === marcaActual);

    // Filtrar modelo
    if (modeloActual !== 'todos')
        lista = lista.filter(p => p.modelo === modeloActual);

    // Filtrar búsqueda
    // Filtrar búsqueda — busca en todas las columnas
    if (busqueda)
        lista = lista.filter(p =>
            p.nombre.toLowerCase().includes(busqueda) ||
            p.referencia.toLowerCase().includes(busqueda) ||
            (p.desc || '').toLowerCase().includes(busqueda) ||
            (p.categoria || '').toLowerCase().includes(busqueda) ||
            (p.marca || '').toLowerCase().includes(busqueda) ||
            (p.modelo || '').toLowerCase().includes(busqueda) ||
            String(p.precio).includes(busqueda)
        );

    // Ordenar
    if (ordenActual === 'precio-asc')  lista.sort((a, b) => a.precio - b.precio);
    else if (ordenActual === 'precio-desc') lista.sort((a, b) => b.precio - a.precio);
    else lista.sort((a, b) => a.nombre.localeCompare(b.nombre));

    const grid = document.getElementById('productosGrid');
    if (!grid) return;

    if (lista.length === 0) {
        grid.innerHTML = `<div style="grid-column:1/-1;padding:40px;text-align:center;color:var(--texto-suave);font-size:.9rem;">
            No se encontraron repuestos con esos filtros.
        </div>`;
        return;
    }

    const bgCls = p => BG_CAT[p.categoria] || 'bg-motor';

    grid.innerHTML = lista.map(p => {
        const inc    = enCarrito(p.id);
        const agotado = p.stock === 0;

        // Línea de marca + modelo (solo muestra lo que existe)
        const marcaModelo = [p.marca, p.modelo].filter(Boolean).join(' · ');
        const sublinea    = marcaModelo
            ? `<div class="cat-card-marca">
                   <span class="chip-marca">🏷 ${p.marca || ''}</span>
                   ${p.modelo ? `<span class="chip-modelo">🚗 ${p.modelo}</span>` : ''}
                   · ${p.categoria}
               </div>`
            : `<div class="cat-card-marca">${p.categoria}</div>`;

        return `
        <div class="cat-card" onclick="irDetalle(${p.id})" style="cursor:pointer">
            <div class="cat-card-img ${bgCls(p)}">
                ${getImagen(p)}
                ${getBadge(p)}
            </div>
            <div class="cat-card-body">
                <div class="cat-card-nombre">${p.nombre}</div>
                ${sublinea}
                <div class="cat-card-precio">${fmtPrecio(p.precio)} <span>COP</span></div>
                <div class="cat-card-btns">
                    <button class="cat-btn-detalles" onclick="event.stopPropagation();irDetalle(${p.id})">
                        Ver detalles
                    </button>
                    <button class="cat-btn-agregar" id="btn-${p.id}"
                            ${agotado ? 'disabled' : ''}
                            onclick="event.stopPropagation();toggleItem(${p.id},'${p.nombre.replace(/'/g,"\\'")}',${p.precio})">
                        ${agotado ? 'Agotado' : inc ? '✓ Agregado' : '+ Cotizar'}
                    </button>
                </div>
            </div>
        </div>`;
    }).join('');
}

// ── Navegar al detalle ───────────────────────────────────
function irDetalle(id) {
    window.location.href = '/Inventario/Detalles/' + id;
}

// ── Carrito / Cotización ─────────────────────────────────
function toggleItem(id, nombre, precio) {
    if (enCarrito(id)) carrito = carrito.filter(c => c.id !== id);
    else               carrito.push({ id, nombre, precio });
    renderCarrito();
    renderGrid();
}

function renderCarrito() {
    const n = carrito.length;
    ['badgeCount','sbCount'].forEach(eid => {
        const el = document.getElementById(eid);
        if (el) el.textContent = n;
    });

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
                        onclick="toggleItem(${c.id},'',0)">✕</button>
            </div>`).join('');

    const total = carrito.reduce((s, c) => s + Number(c.precio), 0);
    const tel   = document.getElementById('totalVal');
    if (tel) tel.textContent = fmtPrecio(total);
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
    busqueda  = inp ? inp.value.toLowerCase().trim() : '';
    renderGrid();
}

function ordenar(v) {
    ordenActual = v;
    renderGrid();
}

function filtrarMarca() {
    marcaActual = document.getElementById('filtroMarca')?.value || 'todas';
    actualizarModelos();
    renderGrid();
}

function filtrarModelo() {
    modeloActual = document.getElementById('filtroModelo')?.value || 'todos';
    renderGrid();
}

// ── Inicialización ───────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    cargarProductos();
    renderCarrito();

    const btn = document.getElementById('toggleCarrito');
    if (btn) btn.addEventListener('click', toggleCarrito);
});
