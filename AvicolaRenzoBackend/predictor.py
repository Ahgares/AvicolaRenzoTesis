import sys
import json
import pandas as pd
import joblib
import matplotlib.pyplot as plt
from matplotlib.dates import DateFormatter

try:
    # Cargar modelo
    modelo = joblib.load("modelo_ventas_simple.pkl")

    # Leer archivo CSV desde argumentos
    if len(sys.argv) < 2:
        raise Exception("Falta ruta del CSV de entrada")
    path = sys.argv[1]
    mode = sys.argv[2].lower() if len(sys.argv) >= 3 else "compare"
    df = pd.read_csv(path)

    # Verificar columnas necesarias
    required = ["inventario_promedio", "precio_kg"]
    if not all(col in df.columns for col in required):
        raise Exception("Faltan columnas requeridas en el CSV: inventario_promedio, precio_kg")

    # Predecir
    X = df[["inventario_promedio", "precio_kg"]]
    pred = modelo.predict(X)

    # Generar grafico con fechas si existen y comparación con inventario
    x = None
    if "fecha" in df.columns:
        try:
            x = pd.to_datetime(df["fecha"])
        except Exception:
            x = None
    if x is None:
        x = range(len(df))

    plt.figure(figsize=(11, 5))
    plt.plot(x, pred, label="Ventas predichas (kg)", marker='o', color='#0d6efd')
    if mode != "single":
        plt.plot(x, df["inventario_promedio"], label="Inventario (kg)", linestyle='--', color='#dc3545')
        plt.title("Predicción de ventas vs Inventario")
    else:
        plt.title("Predicción de ventas")
    plt.ylabel("Kg")
    if isinstance(x, range):
        plt.xlabel("Ítems")
    else:
        plt.xlabel("Fecha")
        try:
            plt.gca().xaxis.set_major_formatter(DateFormatter('%Y-%m-%d'))
        except Exception:
            pass
        plt.gcf().autofmt_xdate(rotation=45, ha='right')
    plt.legend()
    plt.grid(True, alpha=0.3)
    plt.tight_layout()
    plt.savefig("wwwroot/img/grafico_prediccion.png")

    # Generar JSON de salida
    resultado = []
    for i in range(len(df)):
        inventario = float(df.iloc[i]["inventario_promedio"])
        estimado = float(pred[i])
        diferencia = inventario - estimado

        if diferencia < 0:
            alerta = "Riesgo de quiebre de stock"
        elif diferencia > 100:
            alerta = "Sobra excesiva"
        else:
            alerta = "Stock adecuado"

        resultado.append({
            "inventario_promedio": int(round(inventario)),
            "precio_kg": float(df.iloc[i]["precio_kg"]),
            "ventas_pred": round(estimado, 2),
            "abastecer_kg": max(0.0, round(estimado - inventario, 2)),
            "alerta": alerta
        })

    print(json.dumps(resultado, ensure_ascii=False))
except Exception as e:
    print(json.dumps({"error": str(e)}))
