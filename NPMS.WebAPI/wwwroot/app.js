let currentUser = {
  name: "R&D User",
  role: "R&D Team",
  isRd: true
};

let currentView = "list"; // "list", "detail", "form"
let currentProduct = null;
let activeProcessIndex = 0;
let productsData = [];

// Initialize
document.addEventListener("DOMContentLoaded", () => {
  fetchProducts();
});

function toggleUserRole() {
  if (currentUser.isRd) {
    currentUser = { name: "User", role: "Non-R&D Department", isRd: false };
  } else {
    currentUser = { name: "R&D User", role: "R&D Team", isRd: true };
  }
  updateUserUI();
  // Refresh view
  if (currentView === "list") renderListView();
  else if (currentView === "detail" && currentProduct) renderDetailView(currentProduct.productId);
}

function updateUserUI() {
  document.getElementById("sidebar-user-name").textContent = currentUser.name;
  document.getElementById("sidebar-user-role").textContent = currentUser.role;
  document.getElementById("role-toggle-label").textContent = currentUser.isRd ? "Non-R&D" : "R&D Team";
}

async function fetchProducts(query = "", status = "", factory = "") {
  try {
    let url = `/api/products?query=${encodeURIComponent(query)}&status=${encodeURIComponent(status)}&factory=${encodeURIComponent(factory)}`;
    const res = await fetch(url);
    if (res.ok) {
      productsData = await res.json();
      if (currentView === "list") renderListView();
    }
  } catch (err) {
    console.error("Error fetching products:", err);
  }
}

function switchView(view, productId = null) {
  currentView = view;
  if (view === "list") {
    fetchProducts();
  } else if (view === "detail" && productId) {
    renderDetailView(productId);
  } else if (view === "form") {
    renderFormView(productId);
  }
}

function handleTopSearch(e) {
  if (e.key === "Enter" || e.type === "keyup") {
    const val = e.target.value;
    if (currentView !== "list") {
      currentView = "list";
    }
    fetchProducts(val, getFilterVal("filter-status"), getFilterVal("filter-factory"));
  }
}

function getFilterVal(id) {
  const el = document.getElementById(id);
  return el ? el.value : "";
}

function applyFilters() {
  const query = document.getElementById("top-search-input").value;
  const status = document.getElementById("filter-status").value;
  const factory = document.getElementById("filter-factory").value;
  fetchProducts(query, status, factory);
}

function clearFilters() {
  document.getElementById("top-search-input").value = "";
  if (document.getElementById("filter-status")) document.getElementById("filter-status").value = "Status";
  if (document.getElementById("filter-factory")) document.getElementById("filter-factory").value = "Factory";
  if (document.getElementById("filter-part")) document.getElementById("filter-part").value = "Part Number";
  fetchProducts();
}

// Render Product Database Table View (Image 1 Mockup)
function renderListView() {
  const main = document.getElementById("main-content");
  main.innerHTML = `
    <div class="breadcrumb">
      <span>Dashboard</span>
    </div>
    
    <div class="page-header">
      <div class="page-title-group">
        <h1>Product Database</h1>
        <p>Search and manage product information</p>
      </div>
      ${currentUser.isRd ? `<button class="btn btn-primary" onclick="switchView('form')">+ Add New Product</button>` : ''}
    </div>

    <!-- Filter Bar -->
    <div class="filter-bar">
      <select id="filter-part" class="filter-select" onchange="applyFilters()">
        <option>Part Number</option>
        ${productsData.map(p => `<option value="${p.partNumber}">${p.partNumber}</option>`).join('')}
      </select>
      <select id="filter-status" class="filter-select" onchange="applyFilters()">
        <option>Status</option>
        <option value="Released">Released</option>
        <option value="Draft">Draft</option>
        <option value="Obsolete">Obsolete</option>
        <option value="Pending Review">Pending Review</option>
      </select>
      <select id="filter-factory" class="filter-select" onchange="applyFilters()">
        <option>Factory</option>
        <option value="Bintan">Bintan</option>
        <option value="Alpha Facility">Alpha Facility</option>
        <option value="Beta Facility">Beta Facility</option>
        <option value="Gamma Facility">Gamma Facility</option>
        <option value="Delta Facility">Delta Facility</option>
        <option value="Epsilon Facility">Epsilon Facility</option>
      </select>
      <button class="clear-filter-btn" onclick="clearFilters()">Clear Filters</button>
    </div>

    <!-- Data Table -->
    <div class="data-table-container">
      <table class="data-table">
        <thead>
          <tr>
            <th>PART NUMBER</th>
            <th>PRODUCT NAME</th>
            <th>PRODUCT TYPE</th>
            <th>STATUS</th>
            <th>CURRENT REV</th>
            <th>FACTORY</th>
            <th>PROD LINE</th>
            <th>LAST MODIFIED</th>
          </tr>
        </thead>
        <tbody>
          ${productsData.length === 0 ? `
            <tr><td colspan="8" style="text-align:center; padding: 40px; color: var(--text-muted);">No products found matching criteria.</td></tr>
          ` : productsData.map(p => `
            <tr onclick="switchView('detail', ${p.productId})">
              <td class="pn-link">${p.partNumber}</td>
              <td style="font-weight: 600;">${p.productName}</td>
              <td>${p.productType}</td>
              <td>${getStatusBadge(p.status)}</td>
              <td>${p.currentRevision}</td>
              <td>${p.factory}</td>
              <td>${p.productionLine}</td>
              <td>${new Date(p.lastModified).toLocaleDateString()}</td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    </div>
  `;
}

function getStatusBadge(status) {
  let cls = "badge-draft";
  if (status === "Released") cls = "badge-released";
  else if (status === "Obsolete") cls = "badge-obsolete";
  else if (status === "Pending Review" || status === "Draft") cls = "badge-draft";
  return `<span class="badge ${cls}"><span style="font-size:8px;">●</span> ${status}</span>`;
}

// Render Product Dashboard Detail View (Image 2 & 3 Mockups)
async function renderDetailView(productId) {
  const main = document.getElementById("main-content");
  try {
    const res = await fetch(`/api/products/${productId}`);
    if (!res.ok) return;
    currentProduct = await res.json();
    activeProcessIndex = 0;

    const p = currentProduct;
    const spec = p.specification || {};
    const mfg = p.manufacturingInfo || {};
    const processes = p.processes || [];
    const docs = p.documents || [];

    main.innerHTML = `
      <div class="breadcrumb">
        <a href="#" onclick="switchView('list')">Dashboard</a>
        <span>&rsaquo;</span>
        <span>Search Results</span>
        <span>&rsaquo;</span>
        <span style="color: var(--text-main); font-weight: 500;">Product Dashboard</span>
      </div>
      
      <div style="margin-bottom: 16px;">
        <a href="#" onclick="switchView('list')" style="color: var(--primary-color); text-decoration: none; font-size: 13px; font-weight: 500;">&larr; Back to Search Results</a>
      </div>

      <div class="page-header" style="align-items: center;">
        <div class="page-title-group">
          <h1>${p.productName.toUpperCase()}</h1>
        </div>
        <div>
          ${currentUser.isRd ? `
            <button class="btn btn-outline" onclick="switchView('form', ${p.productId})">
              ✏️ Edit Product
            </button>
          ` : `
            <span class="badge badge-readonly">👁️ Read-only Access</span>
          `}
        </div>
      </div>

      <div class="detail-grid">
        <!-- Left Main Column -->
        <div>
          <!-- Product Information Card -->
          <div class="card">
            <div class="product-info-grid">
              <img src="${p.imagePath || '/images/power_inductor.png'}" class="product-img-thumb" alt="Product Image" onerror="this.src='/images/power_inductor.png'">
              <div>
                <div class="info-group">
                  <div class="info-label">PART NUMBER</div>
                  <div class="info-box">${p.partNumber}</div>
                </div>
                <div class="info-group">
                  <div class="info-label">PRODUCT TYPE</div>
                  <div class="info-box">${p.productType}</div>
                </div>
                <div class="info-group">
                  <div class="info-label">CURRENT REVISION</div>
                  <div class="info-box">${p.currentRevision}</div>
                </div>
              </div>
              <div>
                <div class="info-group">
                  <div class="info-label">PRODUCT NAME</div>
                  <div class="info-box">${p.productName}</div>
                </div>
                <div class="info-group">
                  <div class="info-label">STATUS</div>
                  <div class="info-box">${getStatusBadge(p.status)}</div>
                </div>
              </div>
            </div>
            <div class="info-group" style="margin-top: 12px;">
              <div class="info-label">DESCRIPTION</div>
              <div class="info-box">${p.description || 'High performance component for industrial applications.'}</div>
            </div>
          </div>

          <!-- Manufacturing Information Card -->
          <div class="card">
            <div class="card-title">Manufacturing Information</div>
            <div class="form-grid">
              <div class="info-group">
                <div class="info-label">FACTORY</div>
                <div class="info-box">${mfg.factory || 'Bintan'}</div>
              </div>
              <div class="info-group">
                <div class="info-label">PRODUCTION LINE</div>
                <div class="info-box">${mfg.productionLine || 'Line 03'}</div>
              </div>
            </div>
            <div class="info-group">
              <div class="info-label">PRODUCTION TYPE</div>
              <div class="info-box">${mfg.productionType || 'Automated Assembly'}</div>
            </div>
            <div class="info-group">
              <div class="info-label">MANUFACTURING NOTES</div>
              <div class="info-box">${mfg.manufacturingNotes || 'Standard operating procedures apply.'}</div>
            </div>
          </div>

          <!-- Process Flow Card -->
          <div class="card">
            <div class="card-title">Process Flow</div>
            <div class="process-tabs">
              ${processes.map((pr, idx) => `
                <div class="process-tab ${idx === activeProcessIndex ? 'active' : ''}" onclick="selectProcessTab(${idx})">
                  ${pr.processName}
                </div>
              `).join('')}
            </div>

            <div id="process-tab-content">
              ${renderProcessTabContent(processes[activeProcessIndex])}
            </div>
          </div>

          <!-- Supporting Documents Card -->
          <div class="card">
            <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom: 16px;">
              <div class="card-title" style="margin:0;">Supporting Documents</div>
              ${currentUser.isRd ? `
                <button class="btn btn-outline btn-sm" onclick="openModal('upload-modal')">+ Upload Document</button>
              ` : ''}
            </div>
            <div class="data-table-container">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>DOCUMENT NAME</th>
                    <th>DOCUMENT TYPE</th>
                    <th>REVISION</th>
                    <th>FILE NAME</th>
                    <th>ACTIONS</th>
                  </tr>
                </thead>
                <tbody>
                  ${docs.length === 0 ? `
                    <tr><td colspan="5" style="text-align:center; padding: 20px; color: var(--text-muted);">No supporting documents attached.</td></tr>
                  ` : docs.map(d => `
                    <tr>
                      <td style="font-weight:600;">${d.documentName}</td>
                      <td>${d.documentType}</td>
                      <td>${d.revision}</td>
                      <td>${d.fileName}</td>
                      <td>
                        <a href="/api/documents/${d.documentId}/download" target="_blank" class="pn-link" style="margin-right:12px;">Open</a>
                        <a href="/api/documents/${d.documentId}/download" class="pn-link" download>Download</a>
                        ${currentUser.isRd ? `<a href="#" onclick="deleteDoc(${d.documentId})" style="color:#ef4444; margin-left:12px; font-weight:500;">Delete</a>` : ''}
                      </td>
                    </tr>
                  `).join('')}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- Right Column (Sidebar Spec Card) -->
        <div>
          <div class="card">
            <div class="card-title">Product Specification</div>
            
            <div class="spec-group-title">MATERIAL</div>
            <div class="spec-row">
              <span class="label">Material</span>
              <span class="value">${spec.material || 'Ferrite'}</span>
            </div>

            <div class="spec-group-title">DIMENSIONS</div>
            <div class="spec-row">
              <span class="label">Length</span>
              <span class="value">${spec.length || '25 mm'}</span>
            </div>
            <div class="spec-row">
              <span class="label">Width</span>
              <span class="value">${spec.width || '15 mm'}</span>
            </div>
            <div class="spec-row">
              <span class="label">Height</span>
              <span class="value">${spec.height || '10 mm'}</span>
            </div>
            <div class="spec-row">
              <span class="label">Tolerance</span>
              <span class="value">${spec.tolerance || '±0.1 mm'}</span>
            </div>

            <div class="spec-group-title">ELECTRICAL</div>
            <div class="spec-row">
              <span class="label">Inductance</span>
              <span class="value">${spec.inductance || '10 µH'}</span>
            </div>
            <div class="spec-row">
              <span class="label">Rated Current</span>
              <span class="value">${spec.ratedCurrent || '5 A'}</span>
            </div>
            <div class="spec-row">
              <span class="label">DCR</span>
              <span class="value">${spec.dcr || '0.2 Ω'}</span>
            </div>

            <div class="spec-group-title">OPERATING CONDITIONS</div>
            <div class="spec-row">
              <span class="label">Operating Temp</span>
              <span class="value">${spec.operatingTemperature || '-40 to 125 °C'}</span>
            </div>
          </div>
        </div>
      </div>
    `;
  } catch (err) {
    console.error("Error loading detail view:", err);
  }
}

function selectProcessTab(index) {
  activeProcessIndex = index;
  if (!currentProduct || !currentProduct.processes) return;
  const tabs = document.querySelectorAll(".process-tab");
  tabs.forEach((t, idx) => {
    if (idx === index) t.classList.add("active");
    else t.classList.remove("active");
  });
  document.getElementById("process-tab-content").innerHTML = renderProcessTabContent(currentProduct.processes[index]);
}

function renderProcessTabContent(pr) {
  if (!pr) return `<div style="padding: 20px; color: var(--text-muted);">No process details available.</div>`;
  
  const params = pr.parameters || [];
  return `
    <div class="process-details-card">
      <div style="font-weight:700; margin-bottom:12px; font-size:14px;">Process Details</div>
      <div class="form-grid">
        <div class="info-group">
          <div class="info-label">PROCESS NAME</div>
          <div class="info-box">${pr.processName}</div>
        </div>
        <div class="info-group">
          <div class="info-label">MACHINE</div>
          <div class="info-box">${pr.machineName || 'Machine-07'}</div>
        </div>
      </div>
      <div class="info-group">
        <div class="info-label">TOOLING</div>
        <div class="info-box">${pr.toolingName || 'Tool-23'}</div>
      </div>
      <div class="info-group">
        <div class="info-label">PROCESS DESCRIPTION</div>
        <div class="info-box">${pr.processDescription || 'Standard process sequence for production.'}</div>
      </div>
    </div>

    <div class="data-table-container">
      <table class="data-table">
        <thead>
          <tr>
            <th>PARAMETER</th>
            <th>VALUE</th>
            <th>UNIT</th>
          </tr>
        </thead>
        <tbody>
          ${params.length === 0 ? `
            <tr><td colspan="3" style="text-align:center; padding:16px; color:var(--text-muted);">No parameters defined.</td></tr>
          ` : params.map(p => `
            <tr>
              <td style="font-weight:600;">${p.parameterName}</td>
              <td>${p.parameterValue}</td>
              <td>${p.unit}</td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    </div>
  `;
}

// Render Add / Edit Product Form (Image 4 Mockup)
async function renderFormView(productId = null) {
  const main = document.getElementById("main-content");
  let p = {
    partNumber: "",
    productName: "",
    productType: "Inductor",
    status: "Draft",
    currentRevision: "Revision A",
    description: "",
    specification: { material: "Ferrite", length: "25 mm", width: "15 mm", height: "10 mm", tolerance: "±0.1 mm", inductance: "10 µH", ratedCurrent: "5 A", dcr: "0.2 Ω", operatingTemperature: "-40 to 125 °C" },
    manufacturingInfo: { factory: "Bintan", productionLine: "Line 03", productionType: "Automated Assembly", manufacturingNotes: "" },
    processes: [
      { processName: "Winding", processDescription: "Winding copper wire around core", parameters: [{ parameterName: "Tension", parameterValue: "3.0", unit: "N" }] },
      { processName: "Soldering", processDescription: "Soldering terminal leads", parameters: [{ parameterName: "Temp", parameterValue: "260", unit: "°C" }] },
      { processName: "Molding", processDescription: "Epoxy encapsulation", parameters: [{ parameterName: "Temperature", parameterValue: "175", unit: "°C" }, { parameterName: "Pressure", parameterValue: "5.5", unit: "bar" }] }
    ]
  };

  if (productId) {
    const res = await fetch(`/api/products/${productId}`);
    if (res.ok) p = await res.json();
  }

  main.innerHTML = `
    <div class="breadcrumb">
      <a href="#" onclick="switchView('list')">Dashboard</a>
      <span>&rsaquo;</span>
      <span>${productId ? 'Edit Product' : 'Add New Product'}</span>
    </div>

    <div class="page-header">
      <div class="page-title-group">
        <h1>${productId ? 'Edit Product: ' + p.partNumber : 'Add New Product'}</h1>
      </div>
      <div style="display:flex; gap:12px;">
        <button class="btn btn-outline" onclick="switchView('list')">Cancel</button>
        <button class="btn btn-secondary" onclick="saveProductForm(${productId ? productId : 'null'}, 'Draft')">Save as Draft</button>
        <button class="btn btn-primary" onclick="saveProductForm(${productId ? productId : 'null'}, 'Released')">Save Product</button>
      </div>
    </div>

    <div class="detail-grid">
      <!-- Left Form Section -->
      <div>
        <!-- Product Info Form -->
        <div class="card">
          <div class="card-title">Product Information</div>
          <div class="form-grid">
            <div class="form-group">
              <label>Part Number</label>
              <input type="text" id="form-pn" class="form-control" value="${p.partNumber}" placeholder="Enter Part Number" required>
            </div>
            <div class="form-group">
              <label>Product Name</label>
              <input type="text" id="form-name" class="form-control" value="${p.productName}" placeholder="Enter Product Name" required>
            </div>
          </div>
          <div class="form-grid">
            <div class="form-group">
              <label>Product Type</label>
              <select id="form-type" class="form-control">
                <option value="Inductor" ${p.productType === 'Inductor' ? 'selected' : ''}>Inductor</option>
                <option value="Transformer" ${p.productType === 'Transformer' ? 'selected' : ''}>Transformer</option>
                <option value="Sensor" ${p.productType === 'Sensor' ? 'selected' : ''}>Sensor</option>
                <option value="Choke" ${p.productType === 'Choke' ? 'selected' : ''}>Choke</option>
              </select>
            </div>
            <div class="form-group">
              <label>Status</label>
              <select id="form-status" class="form-control">
                <option value="Draft" ${p.status === 'Draft' ? 'selected' : ''}>Draft</option>
                <option value="Released" ${p.status === 'Released' ? 'selected' : ''}>Released</option>
                <option value="Obsolete" ${p.status === 'Obsolete' ? 'selected' : ''}>Obsolete</option>
                <option value="Pending Review" ${p.status === 'Pending Review' ? 'selected' : ''}>Pending Review</option>
              </select>
            </div>
          </div>
          <div class="form-group">
            <label>Current Revision</label>
            <input type="text" id="form-rev" class="form-control" value="${p.currentRevision || 'Revision A'}" placeholder="Enter Revision">
          </div>
          <div class="form-group">
            <label>Description</label>
            <textarea id="form-desc" class="form-control" placeholder="Enter Description">${p.description || ''}</textarea>
          </div>
        </div>

        <!-- Manufacturing Info Form -->
        <div class="card">
          <div class="card-title">Manufacturing Information</div>
          <div class="form-grid">
            <div class="form-group">
              <label>Factory</label>
              <select id="form-factory" class="form-control">
                <option value="Bintan" ${(p.manufacturingInfo?.factory || '') === 'Bintan' ? 'selected' : ''}>Bintan</option>
                <option value="Alpha Facility" ${(p.manufacturingInfo?.factory || '') === 'Alpha Facility' ? 'selected' : ''}>Alpha Facility</option>
                <option value="Beta Facility" ${(p.manufacturingInfo?.factory || '') === 'Beta Facility' ? 'selected' : ''}>Beta Facility</option>
              </select>
            </div>
            <div class="form-group">
              <label>Production Line</label>
              <input type="text" id="form-line" class="form-control" value="${p.manufacturingInfo?.productionLine || 'Line 03'}">
            </div>
          </div>
          <div class="form-group">
            <label>Production Type</label>
            <input type="text" id="form-prodtype" class="form-control" value="${p.manufacturingInfo?.productionType || 'Automated Assembly'}">
          </div>
          <div class="form-group">
            <label>Manufacturing Notes</label>
            <textarea id="form-mfgnotes" class="form-control" placeholder="Enter Manufacturing Notes">${p.manufacturingInfo?.manufacturingNotes || ''}</textarea>
          </div>
        </div>
      </div>

      <!-- Right Column: Specs & Image Form -->
      <div>
        <div class="card">
          <div class="card-title">Finished Product Image</div>
          <div class="img-upload-box" onclick="alert('Image browser dialog opened. Image selected.')">
            <div class="img-upload-icon">🖼️</div>
            <div style="font-size:13px; color:var(--text-muted);">No image selected</div>
            <button class="btn btn-outline btn-sm" style="margin-top:12px;">Browse...</button>
          </div>
        </div>

        <div class="card">
          <div class="card-title">Product Specification</div>
          <div class="form-group">
            <label>Material</label>
            <input type="text" id="spec-material" class="form-control" value="${p.specification?.material || 'Ferrite'}">
          </div>
          <div class="spec-group-title">DIMENSIONS</div>
          <div class="form-group"><label>Length</label><input type="text" id="spec-length" class="form-control" value="${p.specification?.length || '25 mm'}"></div>
          <div class="form-group"><label>Width</label><input type="text" id="spec-width" class="form-control" value="${p.specification?.width || '15 mm'}"></div>
          <div class="form-group"><label>Height</label><input type="text" id="spec-height" class="form-control" value="${p.specification?.height || '10 mm'}"></div>
          <div class="form-group"><label>Tolerance</label><input type="text" id="spec-tolerance" class="form-control" value="${p.specification?.tolerance || '±0.1 mm'}"></div>

          <div class="spec-group-title">ELECTRICAL</div>
          <div class="form-group"><label>Inductance</label><input type="text" id="spec-inductance" class="form-control" value="${p.specification?.inductance || '10 µH'}"></div>
          <div class="form-group"><label>Rated Current</label><input type="text" id="spec-current" class="form-control" value="${p.specification?.ratedCurrent || '5 A'}"></div>
          <div class="form-group"><label>DCR</label><input type="text" id="spec-dcr" class="form-control" value="${p.specification?.dcr || '0.2 Ω'}"></div>

          <div class="spec-group-title">OPERATING CONDITIONS</div>
          <div class="form-group"><label>Operating Temperature</label><input type="text" id="spec-temp" class="form-control" value="${p.specification?.operatingTemperature || '-40 to 125 °C'}"></div>
        </div>
      </div>
    </div>
  `;
}

async function saveProductForm(productId, targetStatus) {
  const pn = document.getElementById("form-pn").value.trim();
  const name = document.getElementById("form-name").value.trim();
  if (!pn || !name) {
    alert("Part Number and Product Name are required.");
    return;
  }

  const dto = {
    productId: productId || 0,
    partNumber: pn,
    productName: name,
    productType: document.getElementById("form-type").value,
    status: targetStatus || document.getElementById("form-status").value,
    currentRevision: document.getElementById("form-rev").value,
    description: document.getElementById("form-desc").value,
    imagePath: "/images/power_inductor.png",
    specification: {
      material: document.getElementById("spec-material").value,
      length: document.getElementById("spec-length").value,
      width: document.getElementById("spec-width").value,
      height: document.getElementById("spec-height").value,
      tolerance: document.getElementById("spec-tolerance").value,
      inductance: document.getElementById("spec-inductance").value,
      ratedCurrent: document.getElementById("spec-current").value,
      dcr: document.getElementById("spec-dcr").value,
      operatingTemperature: document.getElementById("spec-temp").value
    },
    manufacturingInfo: {
      factory: document.getElementById("form-factory").value,
      productionLine: document.getElementById("form-line").value,
      productionType: document.getElementById("form-prodtype").value,
      manufacturingNotes: document.getElementById("form-mfgnotes").value
    },
    processes: [
      {
        processOrder: 1,
        processName: "Winding",
        processDescription: "High precision wire winding process",
        parameters: [{ parameterName: "Spindle Speed", parameterValue: "3500", unit: "rpm" }]
      },
      {
        processOrder: 2,
        processName: "Molding",
        processDescription: "Epoxy encapsulation process",
        parameters: [{ parameterName: "Temperature", parameterValue: "175", unit: "°C" }, { parameterName: "Pressure", parameterValue: "5.5", unit: "bar" }]
      }
    ]
  };

  try {
    let url = "/api/products";
    let method = "POST";
    if (productId) {
      url = `/api/products/${productId}`;
      method = "PUT";
    }

    const res = await fetch(url, {
      method: method,
      headers: {
        "Content-Type": "application/json",
        "X-User-Name": currentUser.name
      },
      body: JSON.stringify(dto)
    });

    if (res.ok) {
      const saved = await res.json();
      alert("Product saved successfully!");
      switchView("detail", saved.productId);
    } else {
      alert("Failed to save product.");
    }
  } catch (err) {
    console.error("Save error:", err);
  }
}

// Modal Helpers
function openModal(id) {
  document.getElementById(id).classList.add("active");
}

function closeModal(id) {
  document.getElementById(id).classList.remove("active");
}

async function submitDocumentUpload() {
  if (!currentProduct) return;
  const name = document.getElementById("doc-name-input").value.trim();
  const type = document.getElementById("doc-type-input").value;
  const rev = document.getElementById("doc-rev-input").value.trim();
  const fileInput = document.getElementById("doc-file-input");

  if (!name || fileInput.files.length === 0) {
    alert("Document name and file selection are required.");
    return;
  }

  const formData = new FormData();
  formData.append("documentName", name);
  formData.append("documentType", type);
  formData.append("revision", rev);
  formData.append("file", fileInput.files[0]);

  try {
    const res = await fetch(`/api/products/${currentProduct.productId}/documents`, {
      method: "POST",
      headers: {
        "X-User-Name": currentUser.name
      },
      body: formData
    });

    if (res.ok) {
      closeModal("upload-modal");
      renderDetailView(currentProduct.productId);
    } else {
      alert("Failed to upload document.");
    }
  } catch (err) {
    console.error("Upload document error:", err);
  }
}

async function deleteDoc(docId) {
  if (!confirm("Are you sure you want to delete this document?")) return;
  try {
    const res = await fetch(`/api/documents/${docId}`, { method: "DELETE" });
    if (res.ok) {
      renderDetailView(currentProduct.productId);
    }
  } catch (err) {
    console.error("Delete doc error:", err);
  }
}
