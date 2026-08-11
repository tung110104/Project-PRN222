// Do dropdown Tinh/Thanh -> Phuong/Xa tu API hanh chinh Viet Nam (provinces.open-api.vn, ban v2 sau sap nhap 2025).
// Dung chung cho form Them san / Sua san. Gia tri luu vao 2 input hidden Province / Ward (luu ten, khong luu code).
const ADDRESS_API = 'https://provinces.open-api.vn/api/v2';

function normalizeAddressName(text) {
    if (!text) return '';
    return text.toString().toLowerCase()
        .replace(/thành phố|tỉnh|quận|huyện|thị xã|phường|xã|thị trấn/g, '')
        .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
        .trim();
}

async function initAddressSelect(currentProvince, currentWard) {
    const provinceSelect = document.getElementById('provinceSelect');
    const wardSelect = document.getElementById('wardSelect');
    const provinceInput = document.getElementById('provinceInput');
    const wardInput = document.getElementById('wardInput');

    if (!provinceSelect || !wardSelect) return;

    if (currentProvince && provinceInput) provinceInput.value = currentProvince;
    if (currentWard && wardInput) wardInput.value = currentWard;

    try {
        const res = await fetch(`${ADDRESS_API}/p/`);
        const provinces = await res.json();
        const normP = normalizeAddressName(currentProvince);
        let matchedProvince = null;

        if (currentProvince) {
            matchedProvince = provinces.find(p => p.name === currentProvince ||
                p.name.trim().toLowerCase() === currentProvince.trim().toLowerCase() ||
                normalizeAddressName(p.name) === normP);
        }

        provinceSelect.innerHTML = '<option value="">-- Chọn Tỉnh/Thành --</option>' +
            provinces.map(p => {
                const isSel = matchedProvince && (p.code === matchedProvince.code);
                return `<option value="${p.code}" data-name="${p.name}" ${isSel ? 'selected' : ''}>${p.name}</option>`;
            }).join('');

        if (matchedProvince) {
            provinceSelect.value = String(matchedProvince.code);
            provinceInput.value = matchedProvince.name;
            await loadWards(matchedProvince.code, currentWard);
        } else if (currentProvince) {
            const customOpt = document.createElement('option');
            customOpt.value = currentProvince;
            customOpt.dataset.name = currentProvince;
            customOpt.textContent = currentProvince;
            customOpt.selected = true;
            provinceSelect.insertBefore(customOpt, provinceSelect.firstChild.nextSibling);
            provinceInput.value = currentProvince;
            if (currentWard) {
                wardSelect.disabled = false;
                wardSelect.innerHTML = `<option value="${currentWard}" data-name="${currentWard}" selected>${currentWard}</option>`;
                wardInput.value = currentWard;
            }
        }
    } catch (e) {
        console.error('Không tải được danh sách tỉnh/thành:', e);
        if (currentProvince) {
            provinceSelect.innerHTML = `<option value="${currentProvince}" data-name="${currentProvince}" selected>${currentProvince}</option>`;
        } else {
            provinceSelect.innerHTML = '<option value="">Lỗi tải API địa chỉ - kiểm tra mạng</option>';
        }
        if (currentWard) {
            wardSelect.disabled = false;
            wardSelect.innerHTML = `<option value="${currentWard}" data-name="${currentWard}" selected>${currentWard}</option>`;
        }
    }

    provinceSelect.addEventListener('change', async () => {
        const opt = provinceSelect.selectedOptions[0];
        provinceInput.value = opt?.dataset.name || '';
        wardInput.value = '';
        if (provinceSelect.value) {
            await loadWards(provinceSelect.value, null);
        } else {
            wardSelect.disabled = true;
            wardSelect.innerHTML = '<option value="">-- Chọn Tỉnh/Thành trước --</option>';
        }
    });

    wardSelect.addEventListener('change', () => {
        wardInput.value = wardSelect.selectedOptions[0]?.dataset.name || '';
    });

    async function loadWards(provinceCode, selectedWardName) {
        wardSelect.disabled = true;
        wardSelect.innerHTML = '<option value="">Đang tải...</option>';
        try {
            const res = await fetch(`${ADDRESS_API}/p/${provinceCode}?depth=2`);
            const data = await res.json();
            const wards = data.wards || [];
            const normW = normalizeAddressName(selectedWardName);
            let matchedWard = null;

            if (selectedWardName) {
                matchedWard = wards.find(w => w.name === selectedWardName ||
                    w.name.trim().toLowerCase() === selectedWardName.trim().toLowerCase() ||
                    normalizeAddressName(w.name) === normW ||
                    w.name.toLowerCase().includes(selectedWardName.toLowerCase()) ||
                    selectedWardName.toLowerCase().includes(w.name.toLowerCase()));
            }

            wardSelect.innerHTML = '<option value="">-- Chọn Phường/Xã --</option>' +
                wards.map(w => {
                    const isSel = matchedWard && (w.code === matchedWard.code);
                    return `<option value="${w.code}" data-name="${w.name}" ${isSel ? 'selected' : ''}>${w.name}</option>`;
                }).join('');
            wardSelect.disabled = false;

            if (matchedWard) {
                wardSelect.value = String(matchedWard.code);
                wardInput.value = matchedWard.name;
            } else if (selectedWardName) {
                const customOpt = document.createElement('option');
                customOpt.value = selectedWardName;
                customOpt.dataset.name = selectedWardName;
                customOpt.textContent = selectedWardName;
                customOpt.selected = true;
                wardSelect.insertBefore(customOpt, wardSelect.firstChild.nextSibling);
                wardInput.value = selectedWardName;
            }
        } catch (e) {
            console.error('Lỗi tải Phường/Xã:', e);
            if (selectedWardName) {
                wardSelect.disabled = false;
                wardSelect.innerHTML = `<option value="${selectedWardName}" data-name="${selectedWardName}" selected>${selectedWardName}</option>`;
                wardInput.value = selectedWardName;
            } else {
                wardSelect.innerHTML = '<option value="">Lỗi tải Phường/Xã</option>';
            }
        }
    }
}
