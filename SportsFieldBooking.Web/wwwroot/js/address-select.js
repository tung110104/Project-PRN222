// Do dropdown Tinh/Thanh -> Phuong/Xa tu API hanh chinh Viet Nam (provinces.open-api.vn, ban v2 sau sap nhap 2025).
// Dung chung cho form Them san / Sua san. Gia tri luu vao 2 input hidden Province / Ward (luu ten, khong luu code).
const ADDRESS_API = 'https://provinces.open-api.vn/api/v2';

async function initAddressSelect(currentProvince, currentWard) {
    const provinceSelect = document.getElementById('provinceSelect');
    const wardSelect = document.getElementById('wardSelect');
    const provinceInput = document.getElementById('provinceInput');
    const wardInput = document.getElementById('wardInput');

    try {
        const res = await fetch(`${ADDRESS_API}/p/`);
        const provinces = await res.json();
        provinceSelect.innerHTML = '<option value="">-- Chọn Tỉnh/Thành --</option>' +
            provinces.map(p => `<option value="${p.code}" data-name="${p.name}">${p.name}</option>`).join('');

        // Truong hop dang sua san: chon lai tinh/phuong da luu
        if (currentProvince) {
            const match = provinces.find(p => p.name === currentProvince);
            if (match) {
                provinceSelect.value = match.code;
                await loadWards(match.code, currentWard);
            }
        }
    } catch (e) {
        provinceSelect.innerHTML = '<option value="">Lỗi tải API địa chỉ - kiểm tra mạng</option>';
        console.error('Không tải được danh sách tỉnh/thành:', e);
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
            wardSelect.innerHTML = '<option value="">-- Chọn Phường/Xã --</option>' +
                wards.map(w => `<option value="${w.code}" data-name="${w.name}">${w.name}</option>`).join('');
            wardSelect.disabled = false;
            if (selectedWardName) {
                const match = wards.find(w => w.name === selectedWardName);
                if (match) wardSelect.value = match.code;
            }
        } catch (e) {
            wardSelect.innerHTML = '<option value="">Lỗi tải Phường/Xã</option>';
            console.error(e);
        }
    }
}
