import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { SearchBox } from "./SearchBox";

describe("SearchBox", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("n'émet la valeur tapée qu'après un silence", () => {
    const onChange = vi.fn();

    render(<SearchBox value="" onChange={onChange} delay={300} />);

    fireEvent.change(screen.getByRole("searchbox"), {
      target: { value: "FR76" },
    });

    expect(onChange).not.toHaveBeenCalled();

    vi.advanceTimersByTime(300);

    expect(onChange).toHaveBeenCalledWith("FR76");
  });

  it("n'émet qu'une fois quand on tape plusieurs caractères d'affilée", () => {
    const onChange = vi.fn();

    render(<SearchBox value="" onChange={onChange} delay={300} />);

    const input = screen.getByRole("searchbox");

    fireEvent.change(input, { target: { value: "F" } });
    vi.advanceTimersByTime(100);
    fireEvent.change(input, { target: { value: "FR" } });
    vi.advanceTimersByTime(300);

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith("FR");
  });
});
