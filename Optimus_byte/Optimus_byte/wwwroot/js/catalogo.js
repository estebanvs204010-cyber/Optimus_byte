const IMG_DEFAULTS = {
    'Motor': '/img/defaults/motor.png',
    'Frenos': '/img/defaults/frenos.png',
    'Filtros': '/img/defaults/filtros.png',
    'Electrico': '/img/defaults/electrico.png',
    'Eléctrico': '/img/defaults/electrico.png',
    'Sistema Eléctrico': '/img/defaults/electrico.png',
    'Suspension': '/img/defaults/suspension.png',
    'Suspensión': '/img/defaults/suspension.png',
    'Lubricantes': '/img/defaults/lubricantes.png',
    '__default': '/img/defaults/repuesto.png'
};

const BG_CAT = {
    'Motor': 'bg-motor',
    'Frenos': 'bg-frenos',
    'Filtros': 'bg-filtros',
    'Electrico': 'bg-electrico',
    'Eléctrico': 'bg-electrico',
    'Sistema Eléctrico': 'bg-electrico',
    'Suspension': 'bg-suspension',
    'Suspensión': 'bg-suspension',
    'Lubricantes': 'bg-lubricante',
};

let productos = [];
let carrito = JSON.parse(localStorage.getItem('carritoOptimus') || '[]');
let catActual = 'todos';
let marcaActual = 'todas';
let modeloActual = 'todos';
let busqueda = '';
let ordenActual = 'nombre';

async function cargarProductos() {
    try {
        const res = await fetch('/Inventario/GetProductos');

        if (!res.ok) {
            throw new Error('No existe /Inventario/GetProductos o devolvio error.');
        }

        productos = await res.json();
        poblarFiltrosMarcaModelo();
        poblarCategoriasSidebar();
        renderGrid();
        renderCarrito();
    } catch (e) {
        console.error('Error al cargar productos:', e);
        const grid = document.getElementById('productosGrid');
        if (grid) {
            grid.innerHTML = `
                <div style="grid-column:1/-1;padding:40px;text-align:center;color:#dc2626;">
                    No se pudieron cargar los productos desde la base de datos.
                </div>`;
        }
    }
}

function guardarCarrito() {
    localStorage.setItem('carritoOptimus', JSON.stringify(carrito));
}

function poblarFiltrosMarcaModelo() {
    const marcas = [...new Set(productos.map(p => p.marca).filter(Boolean))].sort();
    const modelos = [...new Set(productos.map(p => p.modelo).filter(Boolean))].sort();
    const selMarca = document.getElementById('filtroMarca');
    const selModelo = document.getElementById('filtroModelo');

    if (selMarca) {
        selMarca.innerHTML = '<option value="todas">Todas las marcas</option>';
        marcas.forEach(m => {
            const opt = document.createElement('option');
            opt.value = m;
            opt.textContent = m;
            selMarca.appendChild(opt);
        });
    }

    if (selModelo) {
        selModelo.innerHTML = '<option value="todos">Todos los modelos</option>';
        modelos.forEach(m => {
            const opt = document.createElement('option');
            opt.value = m;
            opt.textContent = m;
            selModelo.appendChild(opt);
        });
    }
}

function poblarCategoriasSidebar() {
    const contenedor = document.getElementById('listaCategorias');
    if (!contenedor) return;

    const cats = [...new Set(productos.map(p => p.categoria).filter(Boolean))].sort();

    contenedor.innerHTML = `
        <div class="cat-sb-cat on" onclick="filtrar('todos', this)">
            Todos los productos <span class="arr">→</span>
        </div>`;

    cats.forEach(cat => {
        const div = document.createElement('div');
        div.className = 'cat-sb-cat';
        div.innerHTML = `${cat} <span class="arr">→</span>`;
        div.onclick = function () { filtrar(cat, this); };
        contenedor.appendChild(div);
    });
}

function actualizarModelos() {
    const marcaSel = document.getElementById('filtroMarca')?.value || 'todas';
    const fuente = marcaSel === 'todas' ? productos : productos.filter(p => p.marca === marcaSel);
    const modelos = [...new Set(fuente.map(p => p.modelo).filter(Boolean))].sort();
    const selMod = document.getElementById('filtroModelo');

    if (!selMod) return;

    selMod.innerHTML = '<option value="todos">Todos los modelos</option>';
    modelos.forEach(m => {
        const opt = document.createElement('option');
        opt.value = m;
        opt.textContent = m;
        selMod.appendChild(opt);
    });
    modeloActual = 'todos';
}

function fmtPrecio(p) {
    return '$' + Number(p).toLocaleString('es-CO');
}

function getBadge(p) {
    if (p.stock === 0) return '<span class="cat-badge agotado">Agotado</span>';
    if (p.stock <= 3) return '<span class="cat-badge bajo">Stock bajo</span>';
    return '<span class="cat-badge">Disponible</span>';
}

function enCarrito(id) {
    return carrito.some(c => Number(c.id) === Number(id));
}

function getImagen(p) {
    if (p.imagenUrl && p.imagenUrl.trim() !== '') {
        return `<img src="${p.imagenUrl}" alt="${p.nombre}"
                     style="width:100%;height:100%;object-fit:cover;object-position:center;background:#fff;">`;
    }

    const imgDef = IMG_DEFAULTS[p.categoria] || IMG_DEFAULTS['__default'];
    return `<img src="${imgDef}" alt="${p.categoria}"
                 style="width:65%;height:65%;object-fit:contain;opacity:.5;background:transparent;"
                 onerror="this.parentElement.innerHTML=getIconoFallback('${p.categoria}')">`;
}

function getIconoFallback(cat) {
    const iconos = {
        'Motor': '⚙️',
        'Frenos': '🛞',
        'Filtros': '🌬️',
        'Electrico': '🔋',
        'Eléctrico': '🔋',
        'Sistema Eléctrico': '🔋',
        'Suspension': '🔧',
        'Suspensión': '🔧',
        'Lubricantes': '🛢️',
    };
    return `<span style="font-size:2.8rem;">${iconos[cat] || '🔩'}</span>`;
}

function renderGrid() {
    let lista = [...productos];

    if (catActual !== 'todos') lista = lista.filter(p => p.categoria === catActual);
    if (marcaActual !== 'todas') lista = lista.filter(p => p.marca === marcaActual);
    if (modeloActual !== 'todos') lista = lista.filter(p => p.modelo === modeloActual);

    if (busqueda) {
        lista = lista.filter(p =>
            (p.nombre || '').toLowerCase().includes(busqueda) ||
            (p.referencia || '').toLowerCase().includes(busqueda) ||
            (p.desc || '').toLowerCase().includes(busqueda) ||
            (p.categoria || '').toLowerCase().includes(busqueda) ||
            String(p.precio).includes(busqueda)
        );
    }

    if (ordenActual === 'precio-asc') lista.sort((a, b) => a.precio - b.precio);
    else if (ordenActual === 'precio-desc') lista.sort((a, b) => b.precio - a.precio);
    else lista.sort((a, b) => a.nombre.localeCompare(b.nombre));

    const grid = document.getElementById('productosGrid');
    if (!grid) return;

    if (lista.length === 0) {
        grid.innerHTML = `
            <div style="grid-column:1/-1;padding:40px;text-align:center;color:var(--texto-suave);font-size:.9rem;">
                No se encontraron repuestos con esos filtros.
            </div>`;
        return;
    }

    const bgCls = p => BG_CAT[p.categoria] || 'bg-motor';

    grid.innerHTML = lista.map(p => {
        const inc = enCarrito(p.id);
        const agotado = p.stock === 0;
        const nombreSeguro = String(p.nombre || '').replace(/'/g, "\\'");

        return `
        <div class="cat-card" onclick="irDetalle(${p.id})" style="cursor:pointer">
            <div class="cat-card-img ${bgCls(p)}">
                ${getImagen(p)}
                ${getBadge(p)}
            </div>
            <div class="cat-card-body">
                <div class="cat-card-nombre">${p.nombre}</div>
                <div class="cat-card-marca">${p.referencia} · ${p.categoria}</div>
                <div class="cat-card-precio">${fmtPrecio(p.precio)} <span>COP</span></div>
                <div class="cat-card-btns">
                    <button class="cat-btn-detalles" onclick="event.stopPropagation();irDetalle(${p.id})">
                        Ver detalles
                    </button>
                    <button class="cat-btn-agregar" id="btn-${p.id}"
                            ${agotado ? 'disabled' : ''}
                            onclick="event.stopPropagation();toggleItem(${p.id},'${nombreSeguro}',${p.precio})">
                        ${agotado ? 'Agotado' : inc ? '✓ Agregado' : '+ Agregar'}
                    </button>
                </div>
            </div>
        </div>`;
    }).join('');
}

function irDetalle(id) {
    window.location.href = '/Inventario/Detalles/' + id;
}

function toggleItem(id, nombre, precio) {
    if (enCarrito(id)) {
        carrito = carrito.filter(c => Number(c.id) !== Number(id));
    } else {
        carrito.push({ id, nombre, precio: Number(precio), cantidad: 1 });
    }

    guardarCarrito();
    renderCarrito();
    renderGrid();
}

function renderCarrito() {
    const n = carrito.length;

    ['badgeCount', 'sbCount'].forEach(eid => {
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
                <button class="cat-carrito-item-x" onclick="toggleItem(${c.id},'',0)">✕</button>
            </div>`).join('');

    const total = carrito.reduce((s, c) => s + Number(c.precio), 0);
    const tel = document.getElementById('totalVal');
    if (tel) tel.textContent = fmtPrecio(total);
}

function toggleCarrito() {
    const panel = document.getElementById('carritoPanel');
    if (!panel) return;

    const abierto = panel.style.display !== 'none';
    panel.style.display = abierto ? 'none' : 'block';
    if (!abierto) renderCarrito();
}

function pagarCarrito() {
    if (carrito.length === 0) {
        alert('Agrega al menos un producto al carrito antes de pagar.');
        return;
    }

    const form = document.createElement('form');
    form.method = 'POST';
    form.action = '/Inventario/IniciarPago';

    const token = document.querySelector('input[name="__RequestVerificationToken"]');
    if (token) {
        const tokenInput = document.createElement('input');
        tokenInput.type = 'hidden';
        tokenInput.name = '__RequestVerificationToken';
        tokenInput.value = token.value;
        form.appendChild(tokenInput);
    }

    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = 'itemsJson';
    input.value = JSON.stringify(carrito);

    form.appendChild(input);
    document.body.appendChild(form);
    form.submit();
}

function filtrar(cat, el) {
    catActual = cat;
    document.querySelectorAll('.cat-sb-cat').forEach(e => e.classList.remove('on'));
    if (el) el.classList.add('on');
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

function filtrarMarca() {
    marcaActual = document.getElementById('filtroMarca')?.value || 'todas';
    actualizarModelos();
    renderGrid();
}

function filtrarModelo() {
    modeloActual = document.getElementById('filtroModelo')?.value || 'todos';
    renderGrid();
}

document.addEventListener('DOMContentLoaded', () => {
    cargarProductos();
    renderCarrito();

    const btn = document.getElementById('toggleCarrito');
    if (btn) btn.addEventListener('click', toggleCarrito);

    const btnPagar = document.getElementById('btnPagarCarrito');
    if (btnPagar) btnPagar.addEventListener('click', pagarCarrito);
});
