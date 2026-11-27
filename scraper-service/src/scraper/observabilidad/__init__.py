"""Logs en JSON y correlationId del scraper (docs/PLAN-EFT.md §3)."""

from scraper.observabilidad.logs import con_correlacion, configurar_logs, correlacion_actual

__all__ = ["con_correlacion", "configurar_logs", "correlacion_actual"]
