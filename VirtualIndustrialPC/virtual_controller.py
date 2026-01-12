#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
虛擬工控機系統
根據 OK/NG 組圖生成虛假產品圖像
"""

import os
import json
import shutil
import random
import time
from datetime import datetime
from pathlib import Path
from typing import List, Tuple


class VirtualController:
    """虛擬工控機類"""
    
    def __init__(self, config_path: str = "config.json"):
        """初始化虛擬工控機"""
        self.config_path = config_path
        self.config = self.load_or_create_config()
        self.validate_paths()
    
    def load_or_create_config(self) -> dict:
        """載入或創建配置文件"""
        if os.path.exists(self.config_path):
            print(f"載入配置文件: {self.config_path}")
            with open(self.config_path, 'r', encoding='utf-8') as f:
                return json.load(f)
        else:
            print(f"配置文件不存在，創建默認配置: {self.config_path}")
            default_config = {
                "num_stations": 3,
                "images_per_station": 4,
                "ok_images_path": "ok",
                "ng_images_path": "ng",
                "output_path": "lines",
                "daily_production": 3,
                "ok_rate": 0.5,
                "ng_rate": 0.5,
                "wait_time": 10,
                "station_interval": 5
            }
            with open(self.config_path, 'w', encoding='utf-8') as f:
                json.dump(default_config, f, ensure_ascii=False, indent=2)
            return default_config
    
    def validate_paths(self):
        """驗證 OK/NG 目錄並檢查圖片"""
        ok_path = Path(self.config["ok_images_path"])
        ng_path = Path(self.config["ng_images_path"])
        
        # 檢查 OK 目錄
        if not ok_path.exists():
            print(f"⚠️  警告: OK 圖片目錄不存在: {ok_path}")
        else:
            ok_images = list(ok_path.glob("*.png")) + list(ok_path.glob("*.jpg"))
            if not ok_images:
                print(f"⚠️  警告: OK 圖片目錄中沒有圖片: {ok_path}")
            else:
                print(f"✓ OK 圖片目錄找到 {len(ok_images)} 張圖片: {ok_path}")
        
        # 檢查 NG 目錄
        if not ng_path.exists():
            print(f"⚠️  警告: NG 圖片目錄不存在: {ng_path}")
        else:
            ng_images = list(ng_path.glob("*.png")) + list(ng_path.glob("*.jpg"))
            if not ng_images:
                print(f"⚠️  警告: NG 圖片目錄中沒有圖片: {ng_path}")
            else:
                print(f"✓ NG 圖片目錄找到 {len(ng_images)} 張圖片: {ng_path}")
    
    def get_available_images(self, path: Path) -> List[Path]:
        """獲取目錄中所有可用的圖片"""
        if not path.exists():
            return []
        images = list(path.glob("*.png")) + list(path.glob("*.jpg"))
        return images
    
    def generate_product_code(self, date_str: str, material_num: str, station_num: int, image_num: int) -> str:
        """生成產品編碼
        
        格式: YYYYMMDD + 料號(4位) + S + 工站號 + 圖片編號
        例如: 202512290001S11 表示 2025-12-29, 料號0001, 工站1的第1張圖
        """
        return f"{date_str}{material_num:0>4}S{station_num}{image_num}"
    
    def generate_images(self):
        """生成產品圖像"""
        # 獲取當前日期
        today = datetime.now()
        date_str = today.strftime("%Y%m%d")
        
        # 獲取 OK 和 NG 圖片
        ok_path = Path(self.config["ok_images_path"])
        ng_path = Path(self.config["ng_images_path"])
        ok_images = self.get_available_images(ok_path)
        ng_images = self.get_available_images(ng_path)
        
        if not ok_images and not ng_images:
            print("❌ 錯誤: OK 和 NG 目錄都沒有可用圖片，無法生成產品圖像")
            return
        
        # 創建輸出目錄
        output_path = Path(self.config["output_path"])
        output_path.mkdir(exist_ok=True)
        
        num_stations = self.config["num_stations"]
        images_per_station = self.config["images_per_station"]
        daily_production = self.config["daily_production"]
        wait_time = self.config.get("wait_time", 10)  # 預設 10 秒
        station_interval = self.config.get("station_interval", 1)  # 預設 1 秒
        
        # 獲取 OK 和 NG 比率
        ok_rate = self.config.get("ok_rate")
        ng_rate = self.config.get("ng_rate")
        
        # 如果只設定了 ng_rate，則計算 ok_rate（向後兼容）
        if ok_rate is None and ng_rate is not None:
            ok_rate = 1.0 - ng_rate
        # 如果只設定了 ok_rate，則計算 ng_rate
        elif ok_rate is not None and ng_rate is None:
            ng_rate = 1.0 - ok_rate
        # 如果兩個都沒設定，使用默認值
        elif ok_rate is None and ng_rate is None:
            ok_rate = 0.9
            ng_rate = 0.1
        
        # 正規化比率（確保總和為 1）
        total_rate = ok_rate + ng_rate
        if total_rate > 0:
            ok_rate = ok_rate / total_rate
            ng_rate = ng_rate / total_rate
        else:
            # 如果總和為 0，使用默認值
            ok_rate = 0.9
            ng_rate = 0.1
        
        print(f"\n開始生成產品圖像...")
        print(f"日期: {date_str}")
        print(f"工站數量: {num_stations}")
        print(f"每工站圖片數: {images_per_station}")
        print(f"今日投料數: {daily_production}")
        print(f"OK 率: {ok_rate * 100:.1f}%")
        print(f"NG 率: {ng_rate * 100:.1f}%")
        print(f"進料間隔: {wait_time} 秒")
        print(f"工站處理間隔: {station_interval} 秒")
        
        # 為每個料號生成圖像
        for material_idx in range(1, daily_production + 1):
            material_num = material_idx
            product_dir = output_path / f"{date_str}{material_num:0>4}"
            product_dir.mkdir(exist_ok=True)
            
            print(f"\n生成料號 {material_num:0>4}...")
            
            # 為每個工站生成圖像
            for station_num in range(1, num_stations + 1):
                station_dir = product_dir / f"S{station_num}"
                station_dir.mkdir(exist_ok=True)
                
                # 為每個工站生成指定數量的圖片
                for image_num in range(1, images_per_station + 1):
                    # 每張圖片獨立決定從 OK 還是 NG 選擇（混搭）
                    # 根據設定的比率隨機選擇
                    rand_value = random.random()
                    use_ng = rand_value < ng_rate
                    
                    # 選擇圖片來源
                    if use_ng and ng_images:
                        source_images = ng_images
                        source_type = "NG"
                    elif ok_images:
                        source_images = ok_images
                        source_type = "OK"
                    else:
                        # 如果選擇的來源沒有圖片，使用另一個
                        source_images = ng_images if ng_images else ok_images
                        source_type = "NG" if ng_images else "OK"
                    
                    # 隨機選擇一張源圖片
                    if source_images:
                        source_image = random.choice(source_images)
                        
                        # 生成目標文件名
                        target_filename = f"{image_num}.png"
                        target_path = station_dir / target_filename
                        
                        # 複製圖片
                        shutil.copy2(source_image, target_path)
                        
                        # 生成編碼
                        product_code = self.generate_product_code(
                            date_str, str(material_num), station_num, image_num
                        )
                        print(f"  {product_code} [{source_type}] -> {target_path}")
                
                # 每個工站處理完成後等待指定時間（最後一個工站不需要等待）
                if station_num < num_stations:
                    time.sleep(station_interval)
            
            # 每片進料之間等待指定時間（最後一片不需要等待）
            if material_idx < daily_production:
                print(f"\n等待 {wait_time} 秒後處理下一片進料...")
                time.sleep(wait_time)
        
        print(f"\n✓ 完成！共進行 {daily_production} 個產品的製造")
        print(f"輸出目錄: {output_path.absolute()}")


def main():
    """主函數"""
    print("=" * 50)
    print("虛擬工控機系統")
    print("=" * 50)
    
    controller = VirtualController()
    controller.generate_images()
    
    print("\n" + "=" * 50)


if __name__ == "__main__":
    main()

